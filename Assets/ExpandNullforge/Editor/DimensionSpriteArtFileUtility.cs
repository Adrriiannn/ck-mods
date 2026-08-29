using System;
using System.Collections.Generic;
using System.IO;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>Why a picture could not be taken into a mod's own art folder.</summary>
    internal enum DimensionArtCopyProblem
    {
        None = 0,

        /// <summary>It is not a PNG sitting in the project, so there is no file to copy.</summary>
        NotAPng = 1,

        /// <summary>The project knows about it but the file is not where it says.</summary>
        NotOnDisk = 2,

        /// <summary>It was copied and Unity would not import the copy.</summary>
        ImportFailed = 3
    }

    /// <summary>
    /// The file work every generated sprite asset needs: copying pictures in, importing them the
    /// way Core Keeper imports its own, and listing the result for loading.
    /// </summary>
    /// <remarks>
    /// <para>
    /// COPYING IS NOT TIDINESS, IT IS THE NAMING MECHANISM. Core Keeper stores no names for the
    /// animations inside a sprite asset. It works one out at load by taking the picture's TEXTURE
    /// name, deleting the asset's own name from it and hashing what is left — so an animation is
    /// called "ripe" only because its texture is called "carrot_ripe" inside an asset called
    /// "carrot". A generator therefore cannot use the file an author drew where it lies; it has to
    /// put a copy under a name of its own choosing.
    /// </para>
    /// <para>
    /// Shared by the creature and plant generators rather than written twice, because the failure
    /// modes are identical and silent: an unimported copy is a body that draws nothing, and an
    /// asset missing from the manifest is never compiled into the game's atlas — with no error
    /// either way.
    /// </para>
    /// </remarks>
    internal static class DimensionSpriteArtFileUtility
    {
        /// <summary>The folder inside an output folder where generated art lands.</summary>
        public const string ArtFolderName = "Art";

        /// <summary>
        /// Copies one authored picture to the name the game will read its animation name out of.
        /// </summary>
        /// <remarks>
        /// A source already sitting at the destination is left alone rather than copied onto
        /// itself: regenerating something whose art was picked from a previous generation would
        /// otherwise rewrite the file it is reading.
        /// </remarks>
        public static Texture2D CopyPicture(
            Texture2D source,
            string destinationPath,
            out DimensionArtCopyProblem problem)
        {
            problem = DimensionArtCopyProblem.None;
            if (source == null)
            {
                problem = DimensionArtCopyProblem.NotAPng;
                return null;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                ConfigureImporter(destinationPath, source.width, source.height);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(destinationPath);
            }

            if (string.IsNullOrEmpty(sourcePath) ||
                !sourcePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                problem = DimensionArtCopyProblem.NotAPng;
                return null;
            }

            string sourceAbsolute = AssetPathToAbsolutePath(sourcePath);
            string destinationAbsolute = AssetPathToAbsolutePath(destinationPath);
            if (string.IsNullOrEmpty(sourceAbsolute) ||
                string.IsNullOrEmpty(destinationAbsolute) ||
                !File.Exists(sourceAbsolute))
            {
                problem = DimensionArtCopyProblem.NotOnDisk;
                return null;
            }

            byte[] bytes = File.ReadAllBytes(sourceAbsolute);
            if (!File.Exists(destinationAbsolute) ||
                !SameBytes(File.ReadAllBytes(destinationAbsolute), bytes))
            {
                File.WriteAllBytes(destinationAbsolute, bytes);
                AssetDatabase.ImportAsset(
                    destinationPath, ImportAssetOptions.ForceSynchronousImport);
            }

            ConfigureImporter(destinationPath, source.width, source.height);
            Texture2D copied = AssetDatabase.LoadAssetAtPath<Texture2D>(destinationPath);
            if (copied == null)
            {
                problem = DimensionArtCopyProblem.ImportFailed;
            }

            return copied;
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Imports a copied picture the way the game's own art is imported.
        /// </summary>
        /// <remarks>
        /// Point filtering and no compression are not taste: the whole game is drawn at sixteen
        /// pixels to a tile, and a bilinear or block-compressed sprite reads as a smear beside
        /// everything else on screen.
        /// </remarks>
        public static void ConfigureImporter(string path, int width, int height)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    return;
                }
            }

            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                changed = true;
            }

            if (importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.npotScale = TextureImporterNPOTScale.None;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 16f))
            {
                importer.spritePixelsPerUnit = 16f;
                changed = true;
            }

            int largest = Mathf.Max(width, height);
            if (largest > 0)
            {
                int required = Mathf.NextPowerOfTwo(largest);
                if (required > importer.maxTextureSize)
                {
                    importer.maxTextureSize = required;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Lists a sprite asset in the mod's manifest, which is what loads it at boot.
        /// </summary>
        /// <remarks>
        /// A sprite asset that is not in a manifest is never compiled into the game's atlas, and
        /// anything pointing at an uncompiled asset simply draws nothing. There is no error for it.
        /// </remarks>
        public static void EnsureManifestContains(
            string outputFolder,
            SpriteAssetBase asset,
            Action<string> warn)
        {
            if (asset == null)
            {
                return;
            }

            string modRoot = DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(outputFolder);
            if (string.IsNullOrEmpty(modRoot))
            {
                warn(
                    "could not find the mod its art belongs to, so its animations were not listed " +
                    "for loading. Generate it into a folder inside a mod.");
                return;
            }

            string manifestPath = modRoot + "/SpriteAssetManifest.asset";
            SpriteAssetManifest manifest =
                AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
            if (manifest == null)
            {
                AssetDatabase.StopAssetEditing();
                try
                {
                    manifest = ScriptableObject.CreateInstance<SpriteAssetManifest>();
                    manifest.name = "SpriteAssetManifest";
                    AssetDatabase.CreateAsset(manifest, manifestPath);
                    AssetDatabase.ImportAsset(
                        manifestPath, ImportAssetOptions.ForceSynchronousImport);
                }
                finally
                {
                    AssetDatabase.StartAssetEditing();
                }

                manifest = AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(manifestPath);
                if (manifest == null)
                {
                    warn("could not create the list its animations have to be loaded from.");
                    return;
                }
            }

            if (manifest.spriteAssets == null)
            {
                manifest.spriteAssets = new List<SpriteAssetBase>();
            }

            bool changed = false;
            bool present = false;
            for (int i = manifest.spriteAssets.Count - 1; i >= 0; i--)
            {
                SpriteAssetBase listed = manifest.spriteAssets[i];
                if (listed == null)
                {
                    manifest.spriteAssets.RemoveAt(i);
                    changed = true;
                }
                else if (listed == asset)
                {
                    present = true;
                }
            }

            if (!present)
            {
                manifest.spriteAssets.Add(asset);
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(manifest);
            }
        }

        /// <summary>
        /// Takes away the generated sprite assets, and the pictures belonging to them, that this
        /// generator's art folder still holds and nothing in the project asks for any more.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHAT GOES WRONG WITHOUT IT. Renaming a creature changes the name every one of its art
        /// files is built from, so the old files stay where they are — still listed in the mod's
        /// sprite manifest, still compiled into the game's atlas, and still claiming the data-block
        /// address the old name hashed to. Nothing errors; the mod simply grows a second copy of
        /// every renamed creature's artwork, for ever, and the address table fills with rows no
        /// object will ever ask for. The plant generator grew a sweep of its own for exactly this;
        /// this is the same sweep, written where every art path can reach it.
        /// </para>
        /// <para>
        /// THE MANIFEST IS THE LEDGER, which is what makes deleting safe. A file is only ever
        /// considered when the mod's own <c>SpriteAssetManifest</c> lists it AND it sits inside the
        /// art folder this generator writes into — and every generated sprite asset lands in both,
        /// because <see cref="EnsureManifestContains"/> puts it there. A hand-authored asset would
        /// have to have been manually dropped into a generator's output folder and manually added
        /// to the manifest to be visible here at all. Nothing outside the art folder is ever
        /// touched, and no file is judged by its name alone.
        /// </para>
        /// <para>
        /// A RUN THAT WANTED NOTHING DELETES NOTHING. An aborted or empty generation is not
        /// evidence that a mod's whole art folder is abandoned, and the failure it would cause —
        /// every creature in the project losing its body — is far worse than the leak.
        /// </para>
        /// </remarks>
        /// <param name="outputFolder">The generator's own output folder, which holds the Art folder.</param>
        /// <param name="wantedAssetNames">The sprite-asset names this run means to keep.</param>
        /// <param name="protectedAssetPaths">
        /// Pictures the project still points at. A generated file may be the source an author
        /// picked for the next thing they authored — <see cref="CopyPicture"/> supports exactly
        /// that by leaving a source that already sits at its destination alone — so anything still
        /// referenced is kept even when the asset it was named for is gone.
        /// </param>
        /// <param name="removed">Called once per deleted asset path, for the generator's report.</param>
        public static void RemoveStaleGeneratedArt(
            string outputFolder,
            ICollection<string> wantedAssetNames,
            ICollection<string> protectedAssetPaths,
            Action<string> removed)
        {
            if (string.IsNullOrEmpty(outputFolder) ||
                wantedAssetNames == null || wantedAssetNames.Count == 0)
            {
                return;
            }

            string artFolder = outputFolder + "/" + ArtFolderName;
            if (!AssetDatabase.IsValidFolder(artFolder))
            {
                return;
            }

            string modRoot = DimensionApiModFolderUtility.ResolveModRootFolderForAssetPath(outputFolder);
            if (string.IsNullOrEmpty(modRoot))
            {
                return;
            }

            SpriteAssetManifest manifest = AssetDatabase.LoadAssetAtPath<SpriteAssetManifest>(
                modRoot + "/SpriteAssetManifest.asset");
            if (manifest == null || manifest.spriteAssets == null)
            {
                return;
            }

            string prefix = artFolder + "/";
            List<string> staleNames = new List<string>();
            List<string> survivingNames = new List<string>();

            // Every picture a surviving sprite asset still points at. This is what lets a clip's
            // leftovers be swept as well as a whole creature's: a picture sitting under a name that
            // is still in use, which the asset of that name no longer references, is art for a clip
            // the author has deleted. Read off the asset itself rather than guessed from names, so
            // a build that failed and left the asset untouched protects everything it always did.
            HashSet<string> stillDrawn = new HashSet<string>(StringComparer.Ordinal);

            bool manifestChanged = false;
            for (int i = manifest.spriteAssets.Count - 1; i >= 0; i--)
            {
                SpriteAssetBase listed = manifest.spriteAssets[i];
                if (listed == null)
                {
                    manifest.spriteAssets.RemoveAt(i);
                    manifestChanged = true;
                    continue;
                }

                string listedPath = AssetDatabase.GetAssetPath(listed);
                if (string.IsNullOrEmpty(listedPath) ||
                    !listedPath.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (wantedAssetNames.Contains(listed.name))
                {
                    survivingNames.Add(listed.name);
                    stillDrawn.Add(listedPath);
                    string[] dependencies = AssetDatabase.GetDependencies(listedPath, false);
                    for (int d = 0; d < dependencies.Length; d++)
                    {
                        stillDrawn.Add(dependencies[d]);
                    }

                    continue;
                }

                staleNames.Add(listed.name);
                manifest.spriteAssets.RemoveAt(i);
                manifestChanged = true;
            }

            if (manifestChanged)
            {
                EditorUtility.SetDirty(manifest);
            }

            if (staleNames.Count == 0 && survivingNames.Count == 0)
            {
                return;
            }

            // Sweeping the folder once rather than per name: a creature with a dozen clips has a
            // dozen pictures, and FindAssets is the expensive half of this.
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { artFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) ||
                    !path.StartsWith(prefix, StringComparison.Ordinal) ||
                    stillDrawn.Contains(path) ||
                    (protectedAssetPaths != null && protectedAssetPaths.Contains(path)))
                {
                    continue;
                }

                // Belonging to a name this folder knows is the whole ownership test. A file under
                // no known name is not this generator's to delete — it may be another generator's
                // art, or something an author put here by hand.
                string stem = StemOf(path);
                if (!IsWantedArt(stem, staleNames) && !IsWantedArt(stem, survivingNames))
                {
                    continue;
                }

                if (AssetDatabase.DeleteAsset(path) && removed != null)
                {
                    removed(path);
                }
            }
        }

        /// <summary>
        /// Whether a file name belongs to one of the named sprite assets.
        /// </summary>
        /// <remarks>
        /// A picture is named after the asset it belongs to plus the animation it is, which is not
        /// a convention but the naming MECHANISM: the game works an animation's name out by
        /// deleting the asset's name off the front of the texture's. So "belongs to" is exactly
        /// "is that name, or starts with that name and an underscore".
        /// </remarks>
        private static bool IsWantedArt(string stem, ICollection<string> names)
        {
            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (string.Equals(stem, name, StringComparison.Ordinal) ||
                    stem.StartsWith(name + "_", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The file name without its folder or its extension.</summary>
        private static string StemOf(string assetPath)
        {
            int slash = assetPath.LastIndexOf('/');
            string name = slash < 0 ? assetPath : assetPath.Substring(slash + 1);
            int dot = name.LastIndexOf('.');
            return dot < 0 ? name : name.Substring(0, dot);
        }

        /// <summary>
        /// A stable half of a data-block address, derived from a name.
        /// </summary>
        /// <remarks>
        /// The salt is what keeps one kind of art from ever landing on another's address: every
        /// caller passes its own pair, and the same name under two salts gives two numbers that
        /// have nothing to do with each other.
        /// </remarks>
        public static long StableAddressPart(string value, ulong salt)
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

                if (hash == 0UL)
                {
                    hash = salt | 1UL;
                }

                return (long)hash;
            }
        }

        public static string AssetPathToAbsolutePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            string projectFolder = Path.GetDirectoryName(Application.dataPath);
            return string.IsNullOrEmpty(projectFolder)
                ? string.Empty
                : Path.Combine(projectFolder, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>Makes the art folder exist.</summary>
        /// <remarks>
        /// The body is <see cref="DimensionAssetFolders.Ensure"/>, with every other folder this
        /// framework makes. Kept as a name here because the sprite-art callers ask for it by this
        /// name and it reads as part of writing art files.
        /// </remarks>
        public static void EnsureFolder(string folder)
        {
            DimensionAssetFolders.Ensure(folder);
        }

        /// <summary>A name safe to use as a file name and as a sprite asset's own name.</summary>
        public static string SanitizeName(string value, string fallback)
        {
            if (string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            char[] characters = value.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                char c = characters[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    characters[i] = '_';
                }
            }

            return new string(characters);
        }
    }
}
