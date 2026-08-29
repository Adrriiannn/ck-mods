using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools.Generation
{
    /// <summary>
    /// Loading and comparing Core Keeper's shipped tileset art, shared by the golden menu harness and the
    /// automated generator tests.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exists so the test and the harness prove the same thing. Both compare a regenerated atlas to a
    /// shipped sheet, and both are only meaningful if they decode those sheets identically — two decoders
    /// that disagree about row order or gamma would give two different answers to the same question, and
    /// the one nobody is watching would be the one that drifted.
    /// </para>
    /// <para>
    /// PNGs are decoded by hand rather than through <c>Texture2D.LoadImage</c> + <c>GetPixels32</c>. That
    /// path's orientation and colour handling depend on the Unity version and the asset's import settings,
    /// neither of which this comparison should be at the mercy of. The manual decode yields raw scanline
    /// order (row 0 = top), which is the order the compositor and vanilla's own bake both work in.
    /// </para>
    /// </remarks>
    internal static class DimensionTilesetGoldenAssets
    {
        internal const int Tile = 16;

        /// <summary>
        /// Rebuilds one of Dirt's layers the way Core Keeper baked it, from the captured tables and the
        /// real <c>dirt_tileset.png</c> pixels.
        /// </summary>
        /// <remarks>
        /// The single place that decides how a vanilla layer is recomposited — its connectivity bits and
        /// its output packing. Both the menu harness and the automated parity test call it, so neither
        /// can drift into measuring a differently-composited atlas than the other.
        /// </remarks>
        internal static DimensionTilesetCompositor.GenLayer RegenerateVanillaLayer(
            LayerName layer,
            Color32[] sourcePixels,
            int sourceWidth,
            int sourceHeight)
        {
            DimensionTilesetAtlas.LayerInfo info;
            int connectBits = DimensionTilesetAtlas.TryGetLayer(layer, out info) ? info.ConnectBits : 255;

            // Pack into vanilla's own captured layout so the result is comparable cell-for-cell with the
            // shipped sheet rather than merely equivalent.
            DimensionAtlasGenLayout layout = DimensionAtlasGenLayout.TryCreate(layer);
            DimensionDirtTileSource source =
                new DimensionDirtTileSource(layer, sourcePixels, sourceWidth, sourceHeight);
            return DimensionTilesetCompositor.Composite(source, connectBits, layout);
        }

        /// <summary>
        /// Loads a shipped texture's pixels by file name, in image order (row 0 = top).
        /// </summary>
        /// <remarks>
        /// Reads the file directly, so it works regardless of whether the texture was imported readable.
        /// </remarks>
        internal static bool TryLoadPixels(string fileName, out Color32[] px, out int width, out int height)
        {
            px = null;
            width = 0;
            height = 0;

            string path = FindFile(fileName + ".png");
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch
            {
                return false;
            }

            return TryDecodePng(bytes, out px, out width, out height);
        }

        /// <summary>
        /// Locates a shipped texture in the project or, failing that, in the Core Keeper assets package.
        /// </summary>
        internal static string FindFile(string fileNameWithExt)
        {
            string stem = Path.GetFileNameWithoutExtension(fileNameWithExt);
            foreach (string guid in AssetDatabase.FindAssets(stem + " t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(p) &&
                    Path.GetFileName(p).Equals(fileNameWithExt, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            const string PackageCache = "Library/PackageCache";
            if (Directory.Exists(PackageCache))
            {
                foreach (string f in Directory.GetFiles(PackageCache, fileNameWithExt, SearchOption.AllDirectories))
                {
                    return f;
                }
            }

            return null;
        }

        /// <summary>Copies the 16×16 cell a UV rect points at out of an atlas.</summary>
        internal static Color32[] CellFromUv(Color32[] px, int w, int h, Rect uv)
        {
            int x0 = Mathf.RoundToInt(uv.x * w);
            int y0 = Mathf.RoundToInt(uv.y * h);
            Color32[] cell = new Color32[Tile * Tile];
            for (int y = 0; y < Tile; y++)
            {
                for (int x = 0; x < Tile; x++)
                {
                    int sx = Mathf.Clamp(x0 + x, 0, w - 1);
                    int sy = Mathf.Clamp(y0 + y, 0, h - 1);
                    cell[y * Tile + x] = px[sy * w + sx];
                }
            }

            return cell;
        }

        /// <summary>
        /// Alpha-aware RMSE between a 16×16 tile and the cell at (<paramref name="gx"/>,<paramref name="gy"/>)
        /// in a sheet. Zero means the two are byte-identical.
        /// </summary>
        /// <remarks>
        /// Pixels transparent on both sides are skipped rather than compared: their colour channels hold
        /// whatever the exporter left behind, and two tiles that render identically must not be called
        /// different over bytes no one can see.
        /// </remarks>
        internal static float Rmse(Color32[] tile, Color32[] sheet, int sheetW, int gx, int gy)
        {
            long sum = 0;
            for (int y = 0; y < Tile; y++)
            {
                for (int x = 0; x < Tile; x++)
                {
                    Color32 a = tile[y * Tile + x];
                    Color32 b = sheet[(gy + y) * sheetW + (gx + x)];
                    if (a.a < 128 && b.a < 128)
                    {
                        continue;
                    }

                    int dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b, da = a.a - b.a;
                    sum += dr * dr + dg * dg + db * db + Mathf.Abs(da) * 4;
                }
            }

            return Mathf.Sqrt(sum / (256f * 3f));
        }

        /// <summary>
        /// The closest any 16×16 cell of <paramref name="sheet"/> comes to <paramref name="tile"/>. Zero
        /// means the sheet contains this exact tile somewhere.
        /// </summary>
        internal static float BestRmseAnywhere(Color32[] tile, Color32[] sheet, int sheetW, int sheetH)
        {
            float best = float.MaxValue;
            int cols = sheetW / Tile;
            int rows = sheetH / Tile;
            for (int cy = 0; cy < rows; cy++)
            {
                for (int cx = 0; cx < cols; cx++)
                {
                    float d = Rmse(tile, sheet, sheetW, cx * Tile, cy * Tile);
                    if (d < best)
                    {
                        best = d;
                        if (best <= 0f)
                        {
                            return 0f;
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>In-place vertical flip of a row-major RGBA buffer.</summary>
        internal static void FlipVertical(Color32[] px, int w, int h)
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
        /// Minimal 8-bit RGB/RGBA PNG decoder producing image order (row 0 = top).
        /// </summary>
        internal static bool TryDecodePng(byte[] b, out Color32[] px, out int width, out int height)
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
                using (MemoryStream ms = new MemoryStream(comp, 2, comp.Length - 2)) // skip zlib header
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
                    int filter = raw[y * (stride + 1)];
                    int off = y * (stride + 1) + 1;
                    byte[] cur = new byte[stride];
                    for (int i = 0; i < stride; i++)
                    {
                        int left = i >= bpp ? cur[i - bpp] : 0;
                        int up = prev[i];
                        int upLeft = i >= bpp ? prev[i - bpp] : 0;
                        int x = raw[off + i];
                        int v;
                        switch (filter)
                        {
                            case 1: v = x + left; break;
                            case 2: v = x + up; break;
                            case 3: v = x + ((left + up) >> 1); break;
                            case 4:
                                int predictor = left + up - upLeft;
                                int pa = Math.Abs(predictor - left);
                                int pb = Math.Abs(predictor - up);
                                int pc = Math.Abs(predictor - upLeft);
                                v = x + (pa <= pb && pa <= pc ? left : pb <= pc ? up : upLeft);
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
                    px[i] = new Color32(
                        img[i * bpp],
                        img[i * bpp + 1],
                        img[i * bpp + 2],
                        bpp == 4 ? img[i * bpp + 3] : (byte)255);
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
