using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools.Generation
{
    /// <summary>
    /// Editor-only pixel I/O for the tileset generator.
    ///
    /// Loads sheets by <b>manual PNG decode</b> in image order (row 0 = top) — the convention the
    /// compositor and the offline proof both use — rather than <c>LoadImage</c>+<c>GetPixels32</c>,
    /// whose orientation and gamma handling is Unity-version dependent and cost real debugging time.
    /// Saves baked sheets as PNG assets imported with the crisp point-filter / uncompressed / no-mip
    /// settings the adaptive sampler needs, matching how Core Keeper ships its own GEN sheets.
    /// </summary>
    internal static class DimensionTilesetPixelIo
    {
        /// <summary>Read a texture asset's pixels in image order (row 0 = top) via manual PNG decode.</summary>
        public static bool TryLoadImageOrder(Texture2D texture, out Color32[] px, out int w, out int h)
        {
            px = null;
            w = h = 0;
            if (texture == null)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            return !string.IsNullOrEmpty(path) && TryLoadImageOrderFromFile(path, out px, out w, out h);
        }

        /// <summary>Read a PNG file's pixels in image order (row 0 = top).</summary>
        public static bool TryLoadImageOrderFromFile(string path, out Color32[] px, out int w, out int h)
        {
            px = null;
            w = h = 0;
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch
            {
                return false;
            }

            return TryDecodePng(bytes, out px, out w, out h);
        }

        /// <summary>
        /// Save an image-order RGBA buffer as a PNG asset and import it with crisp adaptive-sampler
        /// settings; returns the imported <see cref="Texture2D"/> (same GUID on re-save), or null on
        /// failure. The buffer is flipped to Unity's bottom-row-first order for <c>SetPixels32</c> so
        /// the encoded PNG stays upright and round-trips byte-for-byte with a manual decode.
        /// </summary>
        public static Texture2D SaveImageOrderPng(Color32[] imageOrderPx, int w, int h, string assetPath)
        {
            Color32[] flipped = (Color32[])imageOrderPx.Clone();
            FlipVertical(flipped, w, h);

            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(flipped);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            try
            {
                File.WriteAllBytes(assetPath, png);
            }
            catch
            {
                return null;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureImport(assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        /// <summary>
        /// Import a tileset sheet the way the game samples it: exact pixels, no filtering, no
        /// compression, no mips. Applied to every sheet the framework writes, and to any sheet the
        /// modder assigns in the Studio — Unity's defaults (bilinear + compressed + mipmapped) blur
        /// and bleed a pixel-art sheet the moment it renders.
        /// </summary>
        public static void ConfigureImport(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        /// <summary>In-place vertical flip of a row-major RGBA buffer.</summary>
        public static void FlipVertical(Color32[] px, int w, int h)
        {
            for (int y = 0; y < h / 2; y++)
            {
                int a = y * w;
                int b = (h - 1 - y) * w;
                for (int x = 0; x < w; x++)
                {
                    Color32 t = px[a + x];
                    px[a + x] = px[b + x];
                    px[b + x] = t;
                }
            }
        }

        /// <summary>
        /// Minimal PNG decoder (8-bit RGB/RGBA) → <see cref="Color32"/>[] in image order (row 0 = top).
        /// A faithful port of the proven offline decoder: parse IHDR/IDAT, inflate past the 2-byte zlib
        /// header, then un-filter the scanlines. Byte-identical to the reference, so comparisons and
        /// bakes are apples-to-apples with no orientation guessing.
        /// </summary>
        public static bool TryDecodePng(byte[] b, out Color32[] px, out int width, out int height)
        {
            px = null;
            width = 0;
            height = 0;
            try
            {
                int p = 8; // skip signature
                int w = 0, h = 0, colorType = 6;
                MemoryStream idat = new MemoryStream();
                while (p + 8 <= b.Length)
                {
                    int len = (b[p] << 24) | (b[p + 1] << 16) | (b[p + 2] << 8) | b[p + 3];
                    string type = Encoding.ASCII.GetString(b, p + 4, 4);
                    if (type == "IHDR")
                    {
                        w = (b[p + 8] << 24) | (b[p + 9] << 16) | (b[p + 10] << 8) | b[p + 11];
                        h = (b[p + 12] << 24) | (b[p + 13] << 16) | (b[p + 14] << 8) | b[p + 15];
                        colorType = b[p + 17];
                    }
                    else if (type == "IDAT")
                    {
                        idat.Write(b, p + 8, len);
                    }
                    else if (type == "IEND")
                    {
                        break;
                    }

                    p += 12 + len;
                }

                int bpp = colorType == 2 ? 3 : 4;
                byte[] comp = idat.ToArray();
                byte[] raw;
                using (MemoryStream ms = new MemoryStream(comp, 2, comp.Length - 2)) // skip zlib header → raw deflate
                using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                using (MemoryStream outMs = new MemoryStream())
                {
                    ds.CopyTo(outMs);
                    raw = outMs.ToArray();
                }

                int stride = w * bpp;
                byte[] img = new byte[h * stride];
                byte[] prev = new byte[stride];
                for (int y = 0; y < h; y++)
                {
                    int ft = raw[y * (stride + 1)];
                    int off = y * (stride + 1) + 1;
                    byte[] cur = new byte[stride];
                    for (int i = 0; i < stride; i++)
                    {
                        int a = i >= bpp ? cur[i - bpp] : 0;
                        int bb = prev[i];
                        int c = i >= bpp ? prev[i - bpp] : 0;
                        int x = raw[off + i];
                        int v;
                        switch (ft)
                        {
                            case 1: v = x + a; break;
                            case 2: v = x + bb; break;
                            case 3: v = x + ((a + bb) >> 1); break;
                            case 4:
                                int pp = a + bb - c;
                                int pa = Math.Abs(pp - a), pb = Math.Abs(pp - bb), pc = Math.Abs(pp - c);
                                v = x + (pa <= pb && pa <= pc ? a : pb <= pc ? bb : c);
                                break;
                            default: v = x; break;
                        }

                        cur[i] = (byte)(v & 255);
                    }

                    Array.Copy(cur, 0, img, y * stride, stride);
                    prev = cur;
                }

                px = new Color32[w * h];
                for (int i = 0; i < w * h; i++)
                {
                    px[i] = new Color32(img[i * bpp], img[i * bpp + 1], img[i * bpp + 2], bpp == 4 ? img[i * bpp + 3] : (byte)255);
                }

                width = w;
                height = h;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
