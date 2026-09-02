using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Which part of a sheet a face reads, and the outline meshes drawn over it.
    /// </summary>
    internal sealed partial class DimensionTilesetBlockPreview
    {
        // ---- wall-cap trim geometry (game-exact) ----

        // Depth of the game's wall top-corner trims/extensions, transcribed from the runtime mesh
        // builder PugMapLayer2._BuildMeshSingle (decompile: ck-db\Pug.Other\PugMapLayer2.cs). The
        // wall layers use the layerIgnoresVertexOffsets branch (capture2: off=(0,0,0), hStretch=0,
        // pad=0, skew=1, ignVtxOff=1).
        //
        // This constant IS the layer's "skewInTopVertices" — the flag's only geometric effect is
        // the shear z' = z - clamp(0.4*y, 0, 1) baked into num9..num12 (lines 983-986), applied to
        // the BACK edge (num9 bottom / num10 top) and, at the extension corners only, past the
        // FRONT edge (num11/num12). With the wall layer's params num8 = offset.y+1+heightStretch
        // = 1, so every moved top corner shifts exactly clamp(0.4*1) = 0.4 toward -z, and the
        // bottom-height shear clamp(0.4*offset.y) = 0 — bottom corners never move. The FRONT face
        // itself is vertical in EVERY variant (legacy PugMapLayerMesh.cs 135-141 unconditionally;
        // PugMapLayer2 1082-1088; 1089-1111 vertical except extension corners), i.e. the slanted
        // face is the game's BACK face — which its fixed camera never shows. UVs stay full-sprite
        // (lines 994-1007) — the trim squashes geometry only. So skew + trims compose to exactly
        // the per-corner table below; nothing further applies for wall layers.
        private const float WallTopTrim = 0.4f;

        /// <summary>
        /// The four top-corner z-shifts (SUBTRACT from each corner's z) for a wall cell, per the
        /// game's ignore-vertex-offsets TOP-face branch (PugMapLayer2._BuildMeshSingle lines
        /// 1039-1073). Axes are the game's own: north = +z = behind, the front face is the -z side.
        /// The decompile's flags map through AdjacentDir: flag=N(64) flag2=S(4) flag3=W(16)
        /// flag4=E(1) flag5=NW(32) flag6=NE(128) flag7=SW(8) flag8=SE(2).
        /// </summary>
        private static void WallCapCornerTrims(int mask, out float swT, out float nwT, out float neT, out float seT)
        {
            bool n = (mask & DirN) != 0;
            bool s = (mask & DirS) != 0;
            bool w = (mask & DirW) != 0;
            bool e = (mask & DirE) != 0;
            bool nw = (mask & DirNW) != 0;
            bool ne = (mask & DirNE) != 0;
            bool sw = (mask & DirSW) != 0;
            bool se = (mask & DirSE) != 0;

            // front-left (v2, lines 1041-1048, "!flag3 && flag2 && flag7"): extends past the front
            // edge when there is no west wall but south AND southwest walls exist — the cap flares
            // forward to meet the trimmed cap of the southwest neighbour.
            swT = !w && s && sw ? WallTopTrim : 0f;
            // back-left (v3, lines 1049-1056, "!flag || (flag && flag3 && !flag5)"): trimmed when
            // there is no north wall, or when north AND west exist but the northwest diagonal is
            // missing (inside corner) — applied even with a north wall present.
            nwT = !n || (n && w && !nw) ? WallTopTrim : 0f;
            // back-right (v4, lines 1057-1064, "!flag || (flag && flag4 && !flag6)"): east/NE mirror.
            neT = !n || (n && e && !ne) ? WallTopTrim : 0f;
            // front-right (v5, lines 1065-1072, "!flag4 && flag2 && flag8"): east/SE mirror of v2.
            seT = !e && s && se ? WallTopTrim : 0f;
        }

        // The game renders the cap as ONE quad split along its SW->NE diagonal (index order
        // v2,v3,v4 / v2,v4,v5 — _BuildMeshSingle lines 988-993), so the corner shifts define a
        // piecewise-linear surface over those two triangles. Evaluating that exact surface keeps
        // the hybrid fallback's four quarter-quads coplanar with the single-quad geometry of the
        // baked-GEN path (each quarter's own SW->NE split lies on or inside the same triangles).
        private static float CapTrimAt(float u, float v, float swT, float nwT, float neT, float seT)
        {
            return v >= u
                ? swT + (nwT - swT) * v + (neT - nwT) * u
                : swT + (seT - swT) * u + (neT - seT) * v;
        }

        // Which cap-corner trim a side face's top vertex follows. The game moves the side faces'
        // top corners under the very same conditions as the matching TOP-face corner
        // (_BuildMeshSingle: FRONT 1089-1111, BACK 1138-1160, LEFT 1179-1201, RIGHT 1220-1242),
        // which is what keeps side tops stitched to the cap. Side edge endpoints are cell corners.
        private static float CornerTrim(Vector2 corner, float swT, float nwT, float neT, float seT)
        {
            if (corner.x < 0f)
            {
                return corner.y < 0f ? swT : nwT;
            }

            return corner.y < 0f ? seT : neT;
        }

        // The raw 8-neighbour "same tile" bitmask (AdjacentDir order), as ResolveQuad builds it.
        private int DirFlags(int cx, int cz, BlockKind kind)
        {
            int f = 0;
            if (Same(cx + 1, cz, kind)) f |= DirE;
            if (Same(cx + 1, cz - 1, kind)) f |= DirSE;
            if (Same(cx, cz - 1, kind)) f |= DirS;
            if (Same(cx - 1, cz - 1, kind)) f |= DirSW;
            if (Same(cx - 1, cz, kind)) f |= DirW;
            if (Same(cx - 1, cz + 1, kind)) f |= DirNW;
            if (Same(cx, cz + 1, kind)) f |= DirN;
            if (Same(cx + 1, cz + 1, kind)) f |= DirNE;
            return f;
        }

        // 16px fallback tiles (used only before the atlas is captured): wall = rows 0-4, ground = rows 4-9.
        private static Rect GroundTile(int rnd)
        {
            return TileUV(rnd % 5, 4 + (rnd / 5) % 5);
        }

        private static Rect WallTile(int rnd)
        {
            return TileUV(rnd % 5, (rnd / 5) % 4);
        }

        private static Rect TileUV(int col, int row)
        {
            // SEAM FIX: the captured STD/SUB/GEN rects all carry vanilla's ~0.125px anti-bleed gutter
            // per side; this procedural fallback grid reconstructed full 16px cells, so its edge
            // samples could interpolate into the neighbouring sheet cell (thin dark seam lines).
            // Apply the same 0.125px-equivalent inset so every sample stays inside the cell.
            const float t = 16f;
            const float inset = 0.125f;
            return new Rect(
                (col * t + inset) / 336f,
                1f - ((row + 1) * t - inset) / 416f,
                (t - 2f * inset) / 336f,
                (t - 2f * inset) / 416f);
        }

        // ---- hologram + selection meshes ----

        private void EnsureHoloMesh(BlockKind kind, bool valid)
        {
            if (holoBuilt && holoKind == kind && holoValid == valid)
            {
                return;
            }

            holoKind = kind;
            holoValid = valid;
            holoBuilt = true;
            DestroyMesh(ref holoMesh);
            Color face = valid ? new Color(0.30f, 0.62f, 1f, 0.30f) : new Color(1f, 0.32f, 0.32f, 0.30f);
            Color top = valid ? new Color(0.55f, 0.8f, 1f, 0.45f) : new Color(1f, 0.5f, 0.5f, 0.45f);
            float height = kind == BlockKind.Wall ? WallCapY : 0.16f;
            holoMesh = BuildHoloBox(height, top, face);
        }

        private void EnsureSelectionMesh()
        {
            if (selectionMesh != null)
            {
                return;
            }

            float height = selectedIsWall ? WallCapY : 0.08f;
            selectionMesh = BuildBoxOutline(height, Color.white, 0.045f);
        }

        // A translucent box (top + four sides) at the unit cell, for the placement hologram.
        private static Mesh BuildHoloBox(float height, Color top, Color side)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<Color> c = new List<Color>();
            List<int> t = new List<int>();
            Rect full = new Rect(0f, 0f, 1f, 1f);
            AddQuad(v, uv, c, t, new Vector3(-0.5f, height, -0.5f), new Vector3(-0.5f, height, 0.5f), new Vector3(0.5f, height, 0.5f), new Vector3(0.5f, height, -0.5f), full, top, top, top, top);
            foreach (Side s in Sides)
            {
                Vector3 a = new Vector3(s.E0.x, 0f, s.E0.y);
                Vector3 b = new Vector3(s.E0.x, height, s.E0.y);
                Vector3 d = new Vector3(s.E1.x, height, s.E1.y);
                Vector3 e = new Vector3(s.E1.x, 0f, s.E1.y);
                AddQuad(v, uv, c, t, a, b, d, e, full, side, side, side, side);
            }

            Mesh mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // The twelve edges of the unit cell box as thin white bars — the selection outline.
        private static Mesh BuildBoxOutline(float height, Color color, float width)
        {
            List<Vector3> v = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<Color> c = new List<Color>();
            List<int> t = new List<int>();
            Vector3[] lo =
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f),
            };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = lo[i];
                Vector3 b = lo[(i + 1) % 4];
                AddBar(v, uv, c, t, a, b, width, color);
                AddBar(v, uv, c, t, a + Vector3.up * height, b + Vector3.up * height, width, color);
                AddBar(v, uv, c, t, a, a + Vector3.up * height, width, color);
            }

            Mesh mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetColors(c);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // A thin camera-agnostic bar between two points, as a quad perpendicular to the world up (or right).
        private static void AddBar(List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> t, Vector3 from, Vector3 to, float width, Color color)
        {
            Vector3 dir = (to - from).normalized;
            Vector3 perp = Vector3.Cross(dir, Vector3.up);
            if (perp.sqrMagnitude < 1e-4f)
            {
                perp = Vector3.right;
            }

            perp = perp.normalized * (width * 0.5f);
            AddQuad(v, uv, c, t, from - perp, from + perp, to + perp, to - perp, new Rect(0f, 0f, 1f, 1f), color, color, color, color);
        }
    }
}
