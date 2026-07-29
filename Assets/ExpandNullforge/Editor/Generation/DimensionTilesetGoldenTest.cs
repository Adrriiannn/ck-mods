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
    /// Validation harness for <see cref="DimensionTilesetCompositor"/>: recomposites the Dirt ground/wall
    /// GEN from the captured tables + real dirt sheet, and pixel-compares it to Core Keeper's SHIPPED GEN
    /// (<c>tileset0_ground_state0.png</c> / <c>tileset0_wall_state0.png</c>). Prints how many of the 256 masks
    /// land pixel-identical vs a real baked cell, and drops the regenerated atlas under
    /// <c>Library/ExpandNullforge/GoldenTest/</c> to eyeball. Expectation (matches the offline Node proof):
    /// most masks RMSE 0; the rest are variant choices (variant 0 only here).
    /// </summary>
    internal static class DimensionTilesetGoldenTest
    {
        [MenuItem("Dimensions API/Debug/Golden Test — Regenerate Dirt GEN")]
        private static void Run()
        {
            if (!DimensionTilesetAtlas.IsReady)
            {
                Debug.LogError("[NF Golden] Atlas not captured — run the mod in-game once to bake DimensionTilesetAtlasData. " + DimensionTilesetAtlas.Status);
                return;
            }

            if (!TryLoadPixels("dirt_tileset", out Color32[] dirt, out int dw, out int dh))
            {
                Debug.LogError("[NF Golden] Could not load dirt_tileset.png from the Core Keeper assets package.");
                return;
            }

            string outDir = "Library/ExpandNullforge/GoldenTest";
            Directory.CreateDirectory(outDir);

            RunLayer(LayerName.ground, "tileset0_ground_state0", dirt, dw, dh, outDir);
            RunLayer(LayerName.wall, "tileset0_wall_state0", dirt, dw, dh, outDir);
            Debug.Log("[NF Golden] Done. Regenerated atlases written under " + outDir + " (open the *_regen.png).");
        }

        private static void RunLayer(LayerName layer, string shippedName, Color32[] dirt, int dw, int dh, string outDir)
        {
            int connect = DimensionTilesetAtlas.TryGetLayer(layer, out DimensionTilesetAtlas.LayerInfo info) ? info.ConnectBits : 255;
            var source = new DimensionDirtTileSource(layer, dirt, dw, dh);
            // Pack into vanilla's exact captured layout (256×H) so the output matches Core Keeper's format 1:1.
            DimensionAtlasGenLayout layout = DimensionAtlasGenLayout.TryCreate(layer);
            DimensionTilesetCompositor.GenLayer gen = DimensionTilesetCompositor.Composite(source, connect, layout);
            Debug.Log("[NF Golden] " + layer + ": packing = " + (layout != null ? "VANILLA layout " + layout.Width + "x" + layout.Height : "own grid"));

            // Save the regenerated atlas for visual inspection (flip image-order → texture y-up so it's upright).
            var savePx = (Color32[])gen.Pixels.Clone();
            FlipVertical(savePx, gen.Width, gen.Height);
            var tex = new Texture2D(gen.Width, gen.Height, TextureFormat.RGBA32, false);
            tex.SetPixels32(savePx);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(outDir, layer + "_regen.png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            if (!TryLoadPixels(shippedName, out Color32[] shipped, out int sw, out int sh))
            {
                Debug.LogWarning("[NF Golden] " + layer + ": regenerated (" + gen.Width + "x" + gen.Height + ") but shipped " + shippedName + ".png not found for comparison.");
                return;
            }

            // ---- DIAGNOSTIC: fingerprints to compare against the Node reference ----
            // Node ref (ground): dirt px00=97,72,34,255; mask0 tile row0R=57,97,97,57,57,97,97,145 sum=105100.
            if (gen.Uvs[0].Count > 0)
            {
                Color32[] t0 = CellFromUv(gen.Pixels, gen.Width, gen.Height, gen.Uvs[0][0]);
                long tsum = 0;
                for (int k = 0; k < t0.Length; k++)
                {
                    tsum += t0[k].r + t0[k].g + t0[k].b;
                }

                float bd = float.MaxValue;
                int bx = 0, by = 0;
                for (int cy = 0; cy * 16 + 16 <= sh; cy++)
                {
                    for (int cx = 0; cx * 16 + 16 <= sw; cx++)
                    {
                        float d = Rmse(t0, shipped, sw, cx * 16, cy * 16);
                        if (d < bd) { bd = d; bx = cx; by = cy; }
                    }
                }

                Debug.Log("[NF DIAG] " + layer + ": dirt " + dirt.Length + "px  dirtPx00=" + dirt[0].r + "," + dirt[0].g + "," + dirt[0].b + "," + dirt[0].a +
                          "  shippedPx00=" + shipped[0].r + "," + shipped[0].g + "," + shipped[0].b +
                          "  | mask0 row0R=" + t0[0].r + "," + t0[1].r + "," + t0[2].r + "," + t0[3].r + "," + t0[4].r + "," + t0[5].r + "," + t0[6].r + "," + t0[7].r +
                          " sum=" + tsum + " bestRMSE=" + bd.ToString("F1") + "@cell" + bx + "," + by);
            }
            // ---- end diagnostic ----

            // For EVERY generated tile (all masks × all variants), find the closest 16×16 cell in the shipped
            // sheet. With full variants each of ours should be pixel-identical to some real vanilla tile.
            int exact = 0, close = 0, total = 0;
            int cols = sw / 16, rows = sh / 16;
            for (int mask = 0; mask < 256; mask++)
            {
                foreach (Rect uv in gen.Uvs[mask])
                {
                    total++;
                    Color32[] tile = CellFromUv(gen.Pixels, gen.Width, gen.Height, uv);
                    float best = float.MaxValue;
                    for (int cy = 0; cy < rows && best > 0.01f; cy++)
                    {
                        for (int cx = 0; cx < cols; cx++)
                        {
                            float d = Rmse(tile, shipped, sw, cx * 16, cy * 16);
                            if (d < best)
                            {
                                best = d;
                            }
                        }
                    }

                    if (best < 6f)
                    {
                        exact++;
                    }

                    if (best < 15f)
                    {
                        close++;
                    }
                }
            }

            Debug.Log("[NF Golden] " + layer + ": of " + total + " tiles (256 masks × variants), pixel-identical(<6)=" + exact +
                      ", close(<15)=" + close + "  (atlas " + gen.Width + "x" + gen.Height + " vs shipped " + sw + "x" + sh + ")");
        }

        private static Color32[] CellFromUv(Color32[] px, int w, int h, Rect uv)
        {
            int x0 = Mathf.RoundToInt(uv.x * w);
            int y0 = Mathf.RoundToInt(uv.y * h);
            var outPx = new Color32[16 * 16];
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    int sx = Mathf.Clamp(x0 + x, 0, w - 1);
                    int sy = Mathf.Clamp(y0 + y, 0, h - 1);
                    outPx[y * 16 + x] = px[sy * w + sx];
                }
            }

            return outPx;
        }

        // Alpha-aware RMSE between a 16×16 tile and a 16×16 cell at (gx,gy) in the shipped sheet.
        private static float Rmse(Color32[] tile, Color32[] sheet, int sheetW, int gx, int gy)
        {
            long s = 0;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    Color32 a = tile[y * 16 + x];
                    Color32 b = sheet[(gy + y) * sheetW + (gx + x)];
                    if (a.a < 128 && b.a < 128)
                    {
                        continue;
                    }

                    int dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b, da = a.a - b.a;
                    s += dr * dr + dg * dg + db * db + Mathf.Abs(da) * 4;
                }
            }

            return Mathf.Sqrt(s / (256f * 3f));
        }

        // Load a texture's pixels by file name (searches the project + PackageCache), bypassing the
        // import 'readable' flag via LoadImage.
        private static bool TryLoadPixels(string fileName, out Color32[] px, out int w, out int h)
        {
            px = null;
            w = h = 0;
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

            // Decode manually (raw PNG scanline order, row 0 = top) rather than LoadImage+GetPixels32, whose
            // orientation/gamma is Unity-version dependent — the manual path is byte-identical to the proven
            // Node harness, so the comparison is apples-to-apples with no flip guessing.
            return TryDecodePng(bytes, out px, out w, out h);
        }

        // Minimal PNG decoder (8-bit RGB/RGBA) → Color32[] in image order (row 0 = top). Editor-only.
        private static bool TryDecodePng(byte[] b, out Color32[] px, out int width, out int height)
        {
            px = null;
            width = 0;
            height = 0;
            try
            {
                int p = 8; // skip signature
                int w = 0, h = 0, colorType = 6;
                var idat = new MemoryStream();
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
                using (var ms = new MemoryStream(comp, 2, comp.Length - 2)) // skip 2-byte zlib header → raw deflate
                using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                using (var outMs = new MemoryStream())
                {
                    ds.CopyTo(outMs);
                    raw = outMs.ToArray();
                }

                int stride = w * bpp;
                var img = new byte[h * stride];
                var prev = new byte[stride];
                for (int y = 0; y < h; y++)
                {
                    int ft = raw[y * (stride + 1)];
                    int off = y * (stride + 1) + 1;
                    var cur = new byte[stride];
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

        // In-place vertical flip of a row-major RGBA buffer.
        private static void FlipVertical(Color32[] px, int w, int h)
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

        private static string FindFile(string fileNameWithExt)
        {
            string stem = Path.GetFileNameWithoutExtension(fileNameWithExt);
            foreach (string guid in AssetDatabase.FindAssets(stem + " t:Texture2D"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(p) && Path.GetFileName(p).Equals(fileNameWithExt, System.StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }

            string pkg = "Library/PackageCache";
            if (Directory.Exists(pkg))
            {
                foreach (string f in Directory.GetFiles(pkg, fileNameWithExt, SearchOption.AllDirectories))
                {
                    return f;
                }
            }

            return null;
        }
    }
}
