using System.Collections.Generic;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Generates smooth, anti-aliased dashboard section icons in code. Each icon is rasterised at
    /// 3× resolution with hard-edged coverage then box-downsampled, so curves and diagonals come
    /// out clean rather than blocky. The icons are drawn white-on-transparent so the caller can
    /// tint them per state. A real PNG dropped into the icons folder overrides the generated one.
    ///
    /// Drop-in override: put a file at
    ///   Assets/ExpandNullforge/Editor/Icons/&lt;sectionId&gt;.png
    /// (overview, dimension, portals, tilesets, layout, biomes, terrain, generation, scenes,
    /// resources, spawns, export, diagnostics) and it replaces the generated icon at full quality.
    /// </summary>
    internal static class DimensionSectionIcons
    {
        private const int Size = 44;   // final icon texture size
        private const int Super = 3;   // supersample factor

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Generated(string sectionId)
        {
            if (Cache.TryGetValue(sectionId, out Texture2D cached) && cached != null)
            {
                return cached;
            }

            Raster r = new Raster(Size, Super);
            Draw(sectionId, r);
            Texture2D tex = r.Resolve();
            Cache[sectionId] = tex;
            return tex;
        }

        private static void Draw(string id, Raster r)
        {
            switch (id)
            {
                case "overview": Binoculars(r); break;
                case "dimension": SplitPlane(r); break;
                case "portals": Portal(r); break;
                case "tilesets": TileGrid(r); break;
                case "layout": Blueprint(r); break;
                case "biomes": Trees(r); break;
                case "terrain": Mountains(r); break;
                case "generation": Spark(r); break;
                case "scenes": ImageStack(r); break;
                case "resources": SwordShield(r); break;
                case "spawns": MonsterFace(r); break;
                case "export": ExportBox(r); break;
                case "diagnostics": Pulse(r); break;
                default: r.Disc(0.5f, 0.5f, 0.18f); break;
            }
        }

        // ---- icon compositions (normalised 0..1, y-down) ----

        private static void Binoculars(Raster r)
        {
            r.Ring(0.33f, 0.6f, 0.16f, 0.16f, 0.035f);
            r.Ring(0.67f, 0.6f, 0.16f, 0.16f, 0.035f);
            r.Line(0.29f, 0.5f, 0.27f, 0.27f, 0.045f);
            r.Line(0.71f, 0.5f, 0.73f, 0.27f, 0.045f);
            r.Line(0.27f, 0.27f, 0.4f, 0.24f, 0.04f);
            r.Line(0.73f, 0.27f, 0.6f, 0.24f, 0.04f);
            r.Line(0.45f, 0.55f, 0.55f, 0.55f, 0.04f);
        }

        private static void SplitPlane(Raster r)
        {
            // A diamond "plane": left face solid, right face hollow — two different sides.
            r.Fill(V(0.5f, 0.12f), V(0.12f, 0.5f), V(0.5f, 0.88f));
            r.Line(0.5f, 0.12f, 0.88f, 0.5f, 0.04f);
            r.Line(0.88f, 0.5f, 0.5f, 0.88f, 0.04f);
            r.Line(0.5f, 0.12f, 0.5f, 0.88f, 0.035f);
        }

        private static void Portal(Raster r)
        {
            r.RingE(0.5f, 0.5f, 0.27f, 0.37f, 0.045f);
            r.RingE(0.5f, 0.5f, 0.15f, 0.21f, 0.04f);
            r.Disc(0.5f, 0.5f, 0.055f);
        }

        private static void TileGrid(Raster r)
        {
            for (int gx = 0; gx < 3; gx++)
            {
                for (int gy = 0; gy < 3; gy++)
                {
                    float x = 0.16f + gx * 0.26f;
                    float y = 0.16f + gy * 0.26f;
                    r.Rect(x, y, 0.18f, 0.18f);
                }
            }
        }

        private static void Blueprint(Raster r)
        {
            r.RectStroke(0.16f, 0.18f, 0.68f, 0.5f, 0.035f);
            r.Line(0.5f, 0.18f, 0.5f, 0.45f, 0.03f);
            r.Line(0.5f, 0.45f, 0.84f, 0.45f, 0.03f);
            r.Line(0.16f, 0.8f, 0.84f, 0.8f, 0.028f); // dimension line
            r.Line(0.16f, 0.76f, 0.16f, 0.84f, 0.028f);
            r.Line(0.84f, 0.76f, 0.84f, 0.84f, 0.028f);
        }

        private static void Trees(Raster r)
        {
            r.Fill(V(0.32f, 0.16f), V(0.16f, 0.56f), V(0.48f, 0.56f));
            r.Line(0.32f, 0.56f, 0.32f, 0.72f, 0.045f);
            r.Fill(V(0.68f, 0.3f), V(0.55f, 0.62f), V(0.81f, 0.62f));
            r.Line(0.68f, 0.62f, 0.68f, 0.76f, 0.04f);
        }

        private static void Mountains(Raster r)
        {
            r.Fill(V(0.12f, 0.82f), V(0.42f, 0.3f), V(0.66f, 0.82f));
            r.Fill(V(0.5f, 0.82f), V(0.72f, 0.42f), V(0.9f, 0.82f));
            // snow notch on the tall peak, punched to transparent
            r.FillClear(V(0.42f, 0.3f), V(0.35f, 0.44f), V(0.5f, 0.44f));
        }

        private static void Spark(Raster r)
        {
            r.Fill(V(0.5f, 0.1f), V(0.57f, 0.5f), V(0.5f, 0.9f), V(0.43f, 0.5f));
            r.Fill(V(0.1f, 0.5f), V(0.5f, 0.57f), V(0.9f, 0.5f), V(0.5f, 0.43f));
        }

        private static void ImageStack(Raster r)
        {
            r.RectStroke(0.16f, 0.16f, 0.42f, 0.42f, 0.03f);
            r.RectStroke(0.28f, 0.28f, 0.42f, 0.42f, 0.03f);
            r.RectStroke(0.4f, 0.4f, 0.44f, 0.44f, 0.03f);
        }

        private static void SwordShield(Raster r)
        {
            // shield
            r.Fill(V(0.42f, 0.2f), V(0.66f, 0.3f), V(0.63f, 0.62f), V(0.42f, 0.8f), V(0.21f, 0.62f), V(0.18f, 0.3f));
            // sword crossing it
            r.Line(0.55f, 0.86f, 0.86f, 0.28f, 0.05f);
            r.Line(0.72f, 0.42f, 0.86f, 0.5f, 0.04f); // crossguard
            r.Line(0.62f, 0.35f, 0.76f, 0.43f, 0.04f);
        }

        private static void MonsterFace(Raster r)
        {
            r.Ring(0.5f, 0.54f, 0.32f, 0.3f, 0.045f);
            r.Disc(0.4f, 0.48f, 0.055f);
            r.Disc(0.6f, 0.48f, 0.055f);
            r.Line(0.36f, 0.66f, 0.43f, 0.72f, 0.035f);
            r.Line(0.43f, 0.72f, 0.5f, 0.66f, 0.035f);
            r.Line(0.5f, 0.66f, 0.57f, 0.72f, 0.035f);
            r.Line(0.57f, 0.72f, 0.64f, 0.66f, 0.035f);
            r.Line(0.34f, 0.28f, 0.42f, 0.36f, 0.04f); // horns
            r.Line(0.66f, 0.28f, 0.58f, 0.36f, 0.04f);
        }

        private static void ExportBox(Raster r)
        {
            // open-top tray
            r.Line(0.22f, 0.52f, 0.22f, 0.84f, 0.04f);
            r.Line(0.22f, 0.84f, 0.78f, 0.84f, 0.04f);
            r.Line(0.78f, 0.84f, 0.78f, 0.52f, 0.04f);
            // arrow leaving upward
            r.Line(0.5f, 0.68f, 0.5f, 0.2f, 0.05f);
            r.Fill(V(0.5f, 0.12f), V(0.63f, 0.34f), V(0.37f, 0.34f));
        }

        private static void Pulse(Raster r)
        {
            r.Line(0.12f, 0.55f, 0.32f, 0.55f, 0.035f);
            r.Line(0.32f, 0.55f, 0.4f, 0.3f, 0.045f);
            r.Line(0.4f, 0.3f, 0.5f, 0.74f, 0.045f);
            r.Line(0.5f, 0.74f, 0.6f, 0.55f, 0.045f);
            r.Line(0.6f, 0.55f, 0.88f, 0.55f, 0.035f);
        }

        private static Vector2 V(float x, float y)
        {
            return new Vector2(x, y);
        }

        /// <summary>Software coverage rasteriser at supersampled resolution.</summary>
        private sealed class Raster
        {
            private readonly int n;
            private readonly int s;
            private readonly float[] cov;

            public Raster(int finalSize, int super)
            {
                n = finalSize;
                s = finalSize * super;
                cov = new float[s * s];
            }

            public void Disc(float nx, float ny, float nr)
            {
                float cx = nx * s;
                float cy = ny * s;
                float rr = nr * s;
                float r2 = rr * rr;
                Scan(cx - rr, cy - rr, cx + rr, cy + rr, (px, py) =>
                {
                    float dx = px - cx;
                    float dy = py - cy;
                    return dx * dx + dy * dy <= r2;
                });
            }

            public void Ring(float nx, float ny, float nrx, float nry, float nhw)
            {
                RingE(nx, ny, nrx, nry, nhw);
            }

            public void RingE(float nx, float ny, float nrx, float nry, float nhw)
            {
                float cx = nx * s;
                float cy = ny * s;
                float rx = nrx * s;
                float ry = nry * s;
                float hw = nhw * s;
                Scan(cx - rx - hw, cy - ry - hw, cx + rx + hw, cy + ry + hw, (px, py) =>
                {
                    float dx = (px - cx) / (rx + hw);
                    float dy = (py - cy) / (ry + hw);
                    float outer = dx * dx + dy * dy;
                    float ix = (px - cx) / Mathf.Max(1f, rx - hw);
                    float iy = (py - cy) / Mathf.Max(1f, ry - hw);
                    float inner = ix * ix + iy * iy;
                    return outer <= 1f && inner >= 1f;
                });
            }

            public void Line(float nx0, float ny0, float nx1, float ny1, float nhw)
            {
                float x0 = nx0 * s;
                float y0 = ny0 * s;
                float x1 = nx1 * s;
                float y1 = ny1 * s;
                float hw = nhw * s;
                float minx = Mathf.Min(x0, x1) - hw;
                float miny = Mathf.Min(y0, y1) - hw;
                float maxx = Mathf.Max(x0, x1) + hw;
                float maxy = Mathf.Max(y0, y1) + hw;
                float dx = x1 - x0;
                float dy = y1 - y0;
                float len2 = Mathf.Max(1e-4f, dx * dx + dy * dy);
                float hw2 = hw * hw;
                Scan(minx, miny, maxx, maxy, (px, py) =>
                {
                    float t = Mathf.Clamp01(((px - x0) * dx + (py - y0) * dy) / len2);
                    float qx = x0 + t * dx - px;
                    float qy = y0 + t * dy - py;
                    return qx * qx + qy * qy <= hw2;
                });
            }

            public void Rect(float nx, float ny, float nw, float nh)
            {
                Scan(nx * s, ny * s, (nx + nw) * s, (ny + nh) * s, (px, py) => true);
            }

            public void RectStroke(float nx, float ny, float nw, float nh, float nhw)
            {
                Line(nx, ny, nx + nw, ny, nhw);
                Line(nx, ny + nh, nx + nw, ny + nh, nhw);
                Line(nx, ny, nx, ny + nh, nhw);
                Line(nx + nw, ny, nx + nw, ny + nh, nhw);
            }

            public void Fill(params Vector2[] pts)
            {
                FillPolygon(pts, 1f);
            }

            public void FillClear(params Vector2[] pts)
            {
                FillPolygon(pts, 0f);
            }

            private void FillPolygon(Vector2[] pts, float value)
            {
                float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;
                Vector2[] p = new Vector2[pts.Length];
                for (int i = 0; i < pts.Length; i++)
                {
                    p[i] = new Vector2(pts[i].x * s, pts[i].y * s);
                    minx = Mathf.Min(minx, p[i].x);
                    miny = Mathf.Min(miny, p[i].y);
                    maxx = Mathf.Max(maxx, p[i].x);
                    maxy = Mathf.Max(maxy, p[i].y);
                }

                int x0 = Mathf.Max(0, Mathf.FloorToInt(minx));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(miny));
                int x1 = Mathf.Min(s - 1, Mathf.CeilToInt(maxx));
                int y1 = Mathf.Min(s - 1, Mathf.CeilToInt(maxy));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if (InConvex(p, x + 0.5f, y + 0.5f))
                        {
                            cov[y * s + x] = value;
                        }
                    }
                }
            }

            private static bool InConvex(Vector2[] p, float px, float py)
            {
                bool hasPos = false;
                bool hasNeg = false;
                for (int i = 0; i < p.Length; i++)
                {
                    Vector2 a = p[i];
                    Vector2 b = p[(i + 1) % p.Length];
                    float cross = (b.x - a.x) * (py - a.y) - (b.y - a.y) * (px - a.x);
                    if (cross > 0.001f)
                    {
                        hasPos = true;
                    }
                    else if (cross < -0.001f)
                    {
                        hasNeg = true;
                    }

                    if (hasPos && hasNeg)
                    {
                        return false;
                    }
                }

                return true;
            }

            private void Scan(float minx, float miny, float maxx, float maxy, System.Func<float, float, bool> inside)
            {
                int x0 = Mathf.Max(0, Mathf.FloorToInt(minx));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(miny));
                int x1 = Mathf.Min(s - 1, Mathf.CeilToInt(maxx));
                int y1 = Mathf.Min(s - 1, Mathf.CeilToInt(maxy));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        if (inside(x + 0.5f, y + 0.5f))
                        {
                            cov[y * s + x] = 1f;
                        }
                    }
                }
            }

            public Texture2D Resolve()
            {
                int super = s / n;
                float norm = 1f / (super * super);
                Color32[] pixels = new Color32[n * n];
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float sum = 0f;
                        for (int sy = 0; sy < super; sy++)
                        {
                            for (int sx = 0; sx < super; sx++)
                            {
                                sum += cov[(y * super + sy) * s + (x * super + sx)];
                            }
                        }

                        byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(sum * norm) * 255f);
                        // Flip Y so texture space (bottom-up) matches the top-down icon space.
                        pixels[(n - 1 - y) * n + x] = new Color32(255, 255, 255, a);
                    }
                }

                Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                    name = "DimensionSectionIcon"
                };
                tex.SetPixels32(pixels);
                tex.Apply(false, false);
                return tex;
            }
        }
    }
}
