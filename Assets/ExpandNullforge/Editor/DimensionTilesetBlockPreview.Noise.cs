using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The simplex noise the vertex jitter is driven by.
    /// </summary>
    internal sealed partial class DimensionTilesetBlockPreview
    {
        // The standard Ashima/webgl-noise 2D simplex ("snoise"), constants verbatim as confirmed
        // in the game's compiled shaders (jitterRecipe):
        //   C = (0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439)
        //   mod289(x) = x - floor(x * (1/289)) * 289 ; permute(x) = mod289(((x*34)+1)*x)
        //   m = max(0.5 - dot(d,d), 0)^4 ; m *= 1.79284291400159 - 0.85373472095314*(a0² + h²)
        //   return 130 * dot(m, g)
        // Scalar port validated offline against an independent vec-for-vec GLSL transcription
        // (max |Δ| 1.7e-5 over 270k samples, range ≈ [-1, 1], mean ≈ 0).
        private static float Snoise2D(float vx, float vy)
        {
            const float Cx = 0.211324865405187f;
            const float Cy = 0.366025403784439f;
            const float Cz = -0.577350269189626f;
            const float Cw = 0.024390243902439f;

            // Skew to simplex cell space; i = base corner, x0 = offset from it.
            float s = (vx + vy) * Cy;
            float ix = Mathf.Floor(vx + s);
            float iy = Mathf.Floor(vy + s);
            float t = (ix + iy) * Cx;
            float x0x = vx - ix + t;
            float x0y = vy - iy + t;

            // Which middle corner of the two simplex triangles the point falls in.
            float i1x = x0x > x0y ? 1f : 0f;
            float i1y = 1f - i1x;

            float x1x = x0x + Cx - i1x;
            float x1y = x0y + Cx - i1y;
            float x2x = x0x + Cz;
            float x2y = x0y + Cz;

            ix = Mod289(ix);
            iy = Mod289(iy);
            float p0 = Permute(Permute(iy) + ix);
            float p1 = Permute(Permute(iy + i1y) + ix + i1x);
            float p2 = Permute(Permute(iy + 1f) + ix + 1f);

            float m0 = Mathf.Max(0.5f - (x0x * x0x + x0y * x0y), 0f);
            float m1 = Mathf.Max(0.5f - (x1x * x1x + x1y * x1y), 0f);
            float m2 = Mathf.Max(0.5f - (x2x * x2x + x2y * x2y), 0f);
            m0 *= m0; m0 *= m0;
            m1 *= m1; m1 *= m1;
            m2 *= m2; m2 *= m2;

            // Gradients from the permutation hash, with the Ashima Taylor inverse-sqrt scaling.
            float g0x = 2f * Frac(p0 * Cw) - 1f;
            float g1x = 2f * Frac(p1 * Cw) - 1f;
            float g2x = 2f * Frac(p2 * Cw) - 1f;
            float h0 = Mathf.Abs(g0x) - 0.5f;
            float h1 = Mathf.Abs(g1x) - 0.5f;
            float h2 = Mathf.Abs(g2x) - 0.5f;
            float a0 = g0x - Mathf.Floor(g0x + 0.5f);
            float a1 = g1x - Mathf.Floor(g1x + 0.5f);
            float a2 = g2x - Mathf.Floor(g2x + 0.5f);
            m0 *= 1.79284291400159f - 0.85373472095314f * (a0 * a0 + h0 * h0);
            m1 *= 1.79284291400159f - 0.85373472095314f * (a1 * a1 + h1 * h1);
            m2 *= 1.79284291400159f - 0.85373472095314f * (a2 * a2 + h2 * h2);

            float d0 = a0 * x0x + h0 * x0y;
            float d1 = a1 * x1x + h1 * x1y;
            float d2 = a2 * x2x + h2 * x2y;
            return 130f * (m0 * d0 + m1 * d1 + m2 * d2);
        }

        private static float Mod289(float x)
        {
            return x - Mathf.Floor(x * (1f / 289f)) * 289f;
        }

        private static float Permute(float x)
        {
            return Mod289((x * 34f + 1f) * x);
        }

        private static float Frac(float x)
        {
            return x - Mathf.Floor(x);
        }

        // Banker's rounding — .NET's Math.Round default — matches HLSL round_ne (round-half-even).
        private static float RoundHalfEven(float v)
        {
            return (float)System.Math.Round(v);
        }
    }
}
