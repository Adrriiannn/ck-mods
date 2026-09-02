using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Building the mesh the preview draws: caps, sides, quarters and the vertex jitter.
    /// </summary>
    internal sealed partial class DimensionTilesetBlockPreview
    {
        // ---- scene mesh (extruded 2.5D geometry) ----

        private void BuildSceneMesh()
        {
            DestroyMesh(ref sceneMesh);
            DestroyMesh(ref stateMesh);
            DisposeGenBatches();
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> cols = new List<Color>();
            List<int> tris = new List<int>();

            // State overlays composite over the caps and carry transparency, so they build a second mesh
            // drawn with the transparent material.
            List<Vector3> sverts = new List<Vector3>();
            List<Vector2> suvs = new List<Vector2>();
            List<Color> scols = new List<Color>();
            List<int> stris = new List<int>();

            foreach (KeyValuePair<Vector2Int, Cell> kv in cells)
            {
                int cx = kv.Key.x;
                int cz = kv.Key.y;
                Cell c = kv.Value;

                // The variant SEED for every layer of this tile is its WORLD POSITION hash — the game's own
                // Random(math.hash(pos)).NextInt(0,256) — a pure function of tile coordinates, never of the
                // neighbour mask, so a tile's piece stays put while its neighbourhood changes. A patterned
                // tileset drops its intended piece for each cell (not a per-placement roll); patternShift
                // offsets the whole grid to shuffle it coherently.
                int variant = DimensionTilesetAtlas.PositionVariant(cx + patternShift * 131, cz + patternShift * 197);
                int wallMask = c.Wall ? DirFlags(cx, cz, BlockKind.Wall) : 0;

                // The game's per-corner wall-top trims for this cell — the cap and every wall side
                // face's top edge share them, which is what keeps the geometry stitched.
                float wallSwT = 0f, wallNwT = 0f, wallNeT = 0f, wallSeT = 0f;
                if (c.Wall)
                {
                    WallCapCornerTrims(wallMask, out wallSwT, out wallNwT, out wallNeT, out wallSeT);
                }

                // Cap: the top layer's single adaptive sprite for this tile's neighbour mask — exactly one
                // sprite per tile, as Core Keeper draws it (never four stitched quarters). A wall hides the
                // ground beneath it, so a wall cell draws only its wall cap at y=1.
                if (c.Wall)
                {
                    AddCap(verts, uvs, cols, tris, cx, cz, WallCapY, LayerName.wall, wallMask, variant, WallTile(variant), CapShade, false);
                }
                else if (c.Ground)
                {
                    AddCap(verts, uvs, cols, tris, cx, cz, GroundCapY, LayerName.ground, DirFlags(cx, cz, BlockKind.Ground), variant, GroundTile(variant), CapShade, false);
                }

                foreach (Side s in Sides)
                {
                    var neighbour = new Vector2Int(cx + s.Dx, cz + s.Dz);
                    bool neighbourWall = HasWall(neighbour.x, neighbour.y);
                    bool neighbourGround = HasGround(neighbour.x, neighbour.y);

                    // wallFront: the above-ground wall side (its own adaptive layer, connecting E/W), shown
                    // wherever the wall is exposed — the 1-tile sprite tiled up the WallTiles-tall face.
                    if (c.Wall && !neighbourWall)
                    {
                        Rect wf = WallFrontUV(wallMask, variant);
                        for (int wy = 0; wy < WallTiles; wy++)
                        {
                            bool topRow = wy == WallTiles - 1; // only the segment touching the cap shears
                            AddSide(verts, uvs, cols, tris, cx, cz, s, GroundCapY + wy, GroundCapY + wy + 1f, wf, FrontShade, FrontShade,
                                topRow ? CornerTrim(s.E0, wallSwT, wallNwT, wallNeT, wallSeT) : 0f,
                                topRow ? CornerTrim(s.E1, wallSwT, wallNwT, wallNeT, wallSeT) : 0f,
                                jitterOn);
                        }
                    }

                    // groundFront: the 3-tile underground cross-section (6 random variants), shown wherever
                    // the ground is exposed — one tall sprite over y=[-3,0], its rock→dirt fade in the art.
                    if (c.Ground && !neighbourGround)
                    {
                        AddSide(verts, uvs, cols, tris, cx, cz, s, GroundFrontBottom, GroundCapY, GroundFrontUV(variant), FrontShade, FrontShade, 0f, 0f, jitterOn);
                    }
                }

                // Ground states overlay the ground cap; wall states paint onto the exposed wall side faces.
                if (c.Ground && !c.Wall && c.GroundStates != null)
                {
                    int kg = 0;
                    for (int i = 0; i < StateCatalog.Length; i++)
                    {
                        StateEntry st = StateCatalog[i];
                        if (!st.IsWall && c.GroundStates.Contains(st.Layer))
                        {
                            AddStateOverlay(sverts, suvs, scols, stris, cx, cz, GroundCapY, st.Layer, false, variant, kg++);
                        }
                    }
                }

                if (c.Wall && c.WallStates != null)
                {
                    int kw = 0;
                    for (int i = 0; i < StateCatalog.Length; i++)
                    {
                        StateEntry st = StateCatalog[i];
                        if (!st.IsWall || !c.WallStates.Contains(st.Layer))
                        {
                            continue;
                        }

                        foreach (Side s in Sides)
                        {
                            if (!HasWall(cx + s.Dx, cz + s.Dz))
                            {
                                AddStateSide(sverts, suvs, scols, stris, cx, cz, s, st.SideLayer, wallMask, variant, kw);
                            }
                        }

                        kw++;
                    }
                }
            }

            sceneMesh = BuildMesh(verts, uvs, cols, tris);
            stateMesh = BuildMesh(sverts, suvs, scols, stris);
            for (int i = 0; i < genBatches.Count; i++)
            {
                GenBatch b = genBatches[i];
                b.Mesh = BuildMesh(b.Verts, b.Uvs, b.Cols, b.Tris);
                b.Material = new Material(b.Transparent ? FindTransparentShader() : FindShader())
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    mainTexture = b.Texture,
                };
            }
        }

        // ---- baked GEN sampling ----

        /// <summary>
        /// The tile UV inside the asset's baked GEN sheet for (layer, mask) — vanilla's exact
        /// resolve: the canonical cell for the mask, column picked by the STABLE position seed modulo
        /// the mask's baked variant count. The seed is a pure function of tile coordinates, so a tile
        /// keeps its piece when neighbours change; only the mask-appropriate cell changes, as in game.
        /// False when the asset has no baked sheet for the layer (fall back to the hybrid resolve).
        ///
        /// SEAM FIX: this used to reconstruct FULL edge-to-edge 16px cell UVs from the cell's pixel
        /// origin — but vanilla samples with its captured ~15.75px rects, inset ~0.125px per side
        /// (the game's anti-bleed gutter). With full-cell UVs, the outermost samples' bilinear/point
        /// footprints reach into the NEIGHBOURING sheet cell (often black wall interior), which drew
        /// thin dark seam lines between adjacent tiles — vertical hairlines down N-S wall columns and
        /// a horizontal line across the ground — made chunky by the point-upscaled low-res RT. Using
        /// the raw captured rect keeps every sample ≥ 0.125px inside its cell, so no cross-cell bleed
        /// at any jitter, zoom or filter. The baked PNGs are written in exactly the captured layout
        /// and size, so the captured rects address them as-is.
        /// </summary>
        private bool TryGetGenUV(LayerName layer, int rawMask, int rnd, out Rect uv)
        {
            uv = default;
            return genTextures.ContainsKey(layer) &&
                   DimensionTilesetAtlas.TryGetGenUVRect(layer, rawMask, rnd, out uv);
        }

        private GenBatch GetGenBatch(LayerName layer, bool transparent)
        {
            for (int i = 0; i < genBatches.Count; i++)
            {
                if (genBatches[i].Layer == layer)
                {
                    return genBatches[i];
                }
            }

            GenBatch b = new GenBatch { Layer = layer, Transparent = transparent, Texture = genTextures[layer] };
            genBatches.Add(b);
            return b;
        }

        private void DisposeGenBatches()
        {
            for (int i = 0; i < genBatches.Count; i++)
            {
                GenBatch b = genBatches[i];
                DestroyMesh(ref b.Mesh);
                DestroyMaterial(ref b.Material);
            }

            genBatches.Clear();
        }

        // One state overlay: a flat sprite on the block's cap, slightly raised and stacked by index so
        // multiple states don't z-fight. Adaptive states go through the same hybrid cap resolve (authored full
        // sprite, else sub-tile composite); RandomFill states pick one of their variants by the cell seed.
        private void AddStateOverlay(
            List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> t,
            int cx, int cz, float baseY, LayerName state, bool isWall, int rnd, int k)
        {
            float y = baseY + 0.02f + k * 0.015f;
            int mask = StateMask(cx, cz, state, isWall);
            if (DimensionTilesetAtlas.TryGetLayer(state, out DimensionTilesetAtlas.LayerInfo info) && info.Adaptive)
            {
                AddCap(v, uv, c, t, cx, cz, y, state, mask, rnd, new Rect(0f, 0f, 0f, 0f), 1f, true);
            }
            else if (DimensionTilesetAtlas.TryGetSpriteUV(state, mask, rnd, out Rect sprite))
            {
                AddTop(v, uv, c, t, cx, cz, y, sprite, 1f, 0f, 0f, 0f, 0f, jitterOn);
            }
        }

        // A cap face (ground/wall top, adaptive state overlay). Core Keeper bakes all 256 masks into one
        // full-adaptive texture and draws a SINGLE sprite per tile (QuadGeneratorExtensions.ResolveQuad).
        // When the asset has generated its own GEN sheet for this layer, the cap samples exactly that: one
        // full 16px tile per mask, all 256 covered — never a composite, a table gap, or a fallback — so the
        // preview shows the very pixels the game will render. Only when the layer has no baked sheet yet
        // does the pre-generator hybrid run: the masks the artist authored in the 9-way table — interiors,
        // straight edges, convex corners — are whole tiles, so we lay one full sprite for them; the masks
        // the table does NOT author — concave INNER corners, thin-wall segments, end-caps — are composited
        // from four sub-tile quarters, as CK's own bake fills them.
        private void AddCap(
            List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> t,
            int cx, int cz, float y, LayerName layer, int rawMask, int rnd, Rect fallback, float shade, bool transparentGen)
        {
            // Wall caps carry the game's per-corner vertex trims (raw mask, not the sprite-table
            // conditioned one — ResolveQuad sets adjacentTilesMask for every adjacent wall before
            // any tileset filtering, PugMapLayer2.cs line 781).
            float swT = 0f, nwT = 0f, neT = 0f, seT = 0f;
            if (layer == LayerName.wall)
            {
                WallCapCornerTrims(rawMask, out swT, out nwT, out neT, out seT);
            }

            // CAP JITTER — settled model (three lines of evidence): wall caps DO jitter, per
            // vertex, exactly like everything else. (1) In-game evidence: caps visibly move WITH
            // their wall columns — a static cap opens gaps against the jittered side-face tops.
            // (2) The TopWall vertex program the game runs applies the noise offset scaled only by
            // _ApplicationIsPlaying — "mad r0.xyz, r0.xyzx, cb0[132].xxxx, r1.xyzx" then
            // "add r0.xyz, r0.xyzx, v0.xyzx" (topwall disasm, program 1) — with NO per-material
            // multiplier; the _EmissiveTexMultiplier-scaled path (×0 in TopWall.mat) exists only in
            // a different pass/permutation and misled an earlier revision into freezing the caps.
            // (3) The wall column-snap sampling rule is the IDENTITY for every cap vertex — cap
            // corners sit on half-integer grid corners, and 0.4-trimmed corners at z = k+0.1 fall
            // inside the snap's exemption window — so "caps sample their own position" (the
            // TopWall shader's rule) and "caps inherit the side-top column offsets" are the same
            // model: shared corners keep cap and side faces sealed, and each cap shears with its
            // wall run. The art shifting ±2 px at junctions is the game's own look.
            // Baked GEN → one quad from the layer's own sheet (its texture differs from the main sheet,
            // so the quad joins that layer's dedicated batch instead of the caller's mesh).
            if (TryGetGenUV(layer, rawMask, rnd, out Rect gen))
            {
                GenBatch batch = GetGenBatch(layer, transparentGen);
                AddTop(batch.Verts, batch.Uvs, batch.Cols, batch.Tris, cx, cz, y, gen, shade, swT, nwT, neT, seT, jitterOn);
                return;
            }

            int mask = rawMask;
            if (DimensionTilesetAtlas.TryGetLayer(layer, out DimensionTilesetAtlas.LayerInfo info))
            {
                mask &= info.ConnectBits;
            }

            // Authored mask → the one full tile the game would draw.
            if (DimensionTilesetAtlas.TryGetAdaptiveUV(layer, mask, rnd, out Rect full))
            {
                AddTop(v, uv, c, t, cx, cz, y, full, shade, swT, nwT, neT, seT, jitterOn);
                return;
            }

            // Unauthored mask → composite the four sub-tile quarters (each quadrant's sub-corner mask), the
            // same pieces CK's bake would stitch. World NW/NE/SW/SE ← TL/TR/BL/BR sub-corner sprites.
            AddSubQuad(v, uv, c, t, cx - 0.5f, cx, cz, cz + 0.5f, y, layer, SubTL(mask), rnd, 0, 1, fallback, shade, swT, nwT, neT, seT, jitterOn); // NW
            AddSubQuad(v, uv, c, t, cx, cx + 0.5f, cz, cz + 0.5f, y, layer, SubTR(mask), rnd, 1, 1, fallback, shade, swT, nwT, neT, seT, jitterOn); // NE
            AddSubQuad(v, uv, c, t, cx - 0.5f, cx, cz - 0.5f, cz, y, layer, SubBL(mask), rnd, 0, 0, fallback, shade, swT, nwT, neT, seT, jitterOn); // SW
            AddSubQuad(v, uv, c, t, cx, cx + 0.5f, cz - 0.5f, cz, y, layer, SubBR(mask), rnd, 1, 0, fallback, shade, swT, nwT, neT, seT, jitterOn); // SE
        }

        // One 8x8 sub-tile quarter drawn onto a half-tile quadrant. Falls back to the matching quarter of the
        // plain tile if the sub-tile table has no entry (rare), else skips.
        private static void AddSubQuad(
            List<Vector3> v, List<Vector2> uv, List<Color> c, List<int> t,
            float x0, float x1, float z0, float z1, float y, LayerName layer, int subMask, int rnd, int qx, int qz, Rect fallback, float shade,
            float swT = 0f, float nwT = 0f, float neT = 0f, float seT = 0f, bool jitterVerts = false)
        {
            if (!DimensionTilesetAtlas.TryGetSubtileUV(layer, subMask, rnd, out Rect r))
            {
                if (fallback.width > 0f && fallback.height > 0f)
                {
                    r = Quarter(fallback, qx, qz);
                }
                else
                {
                    return;
                }
            }

            // Each quarter corner samples the CELL's trim surface at its parametric spot (qx/qz name
            // the quadrant: u,v in half steps), so the four quarters reproduce the game's single
            // trimmed quad exactly — no seams, no plane breaks.
            float u0 = qx * 0.5f;
            float v0 = qz * 0.5f;
            Vector3 sw = new Vector3(x0, y, z0 - CapTrimAt(u0, v0, swT, nwT, neT, seT));
            Vector3 nw = new Vector3(x0, y, z1 - CapTrimAt(u0, v0 + 0.5f, swT, nwT, neT, seT));
            Vector3 ne = new Vector3(x1, y, z1 - CapTrimAt(u0 + 0.5f, v0 + 0.5f, swT, nwT, neT, seT));
            Vector3 se = new Vector3(x1, y, z0 - CapTrimAt(u0 + 0.5f, v0, swT, nwT, neT, seT));
            if (jitterVerts)
            {
                // Cap rule (sample own position). Adjacent quarters — of this cell or a neighbour
                // cell — emit identical corner/mid-edge positions, so they get identical n and
                // stay sealed against each other. (These hybrid-only mid-edge verts have no
                // counterpart in the game's single-quad caps; see the jitter block comment.)
                sw = JitterVert(sw, false);
                nw = JitterVert(nw, false);
                ne = JitterVert(ne, false);
                se = JitterVert(se, false);
            }

            Color col = new Color(shade, shade, shade, 1f);
            AddQuad(v, uv, c, t, sw, nw, ne, se, r, col, col, col, col);
        }

        // CK's per-quadrant canonical sub-corner masks (AdjacentDir.Get*SubCornerBitMask), verbatim.
        private static int SubTL(int d) { int n = 7; if ((d & DirW) != 0) n |= DirW | DirSW; if ((d & DirN) != 0) n |= DirN | DirNE; if ((d & DirNW) != 0) n |= DirNW; return n; }

        private static int SubTR(int d) { int n = 28; if ((d & DirE) != 0) n |= DirE | DirSE; if ((d & DirN) != 0) n |= DirN | DirNW; if ((d & DirNE) != 0) n |= DirNE; return n; }

        private static int SubBL(int d) { int n = 193; if ((d & DirW) != 0) n |= DirW | DirNW; if ((d & DirS) != 0) n |= DirS | DirSE; if ((d & DirSW) != 0) n |= DirSW; return n; }

        private static int SubBR(int d) { int n = 112; if ((d & DirE) != 0) n |= DirE | DirNE; if ((d & DirS) != 0) n |= DirS | DirSW; if ((d & DirSE) != 0) n |= DirSE; return n; }

        private static Rect Quarter(Rect r, int qx, int qz)
        {
            float hw = r.width * 0.5f;
            float hh = r.height * 0.5f;
            return new Rect(r.xMin + qx * hw, r.yMin + qz * hh, hw, hh);
        }

        // A wall state painted onto an exposed wall side face (its "…Front" layer) over the base wallFront,
        // lifted slightly outward and stacked so multiple states don't z-fight.
        private void AddStateSide(
            List<Vector3> v, List<Vector2> uv, List<Color> cols, List<int> t,
            int cx, int cz, Side s, LayerName sideLayer, int mask, int rnd, int k)
        {
            if (!DimensionTilesetAtlas.TryGetSpriteUV(sideLayer, mask, rnd, out Rect sprite))
            {
                return;
            }

            // State overlays follow the WALL's trim geometry (mask here is the wall mask), so they
            // stay glued to the sheared side face instead of poking past its trimmed top edge.
            WallCapCornerTrims(mask, out float swT, out float nwT, out float neT, out float seT);
            Vector3 n = new Vector3(s.Dx, 0f, s.Dz) * (0.01f + k * 0.008f);
            for (int wy = 0; wy < WallTiles; wy++)
            {
                bool topRow = wy == WallTiles - 1;
                float t0 = topRow ? CornerTrim(s.E0, swT, nwT, neT, seT) : 0f;
                float t1 = topRow ? CornerTrim(s.E1, swT, nwT, neT, seT) : 0f;
                float y0 = GroundCapY + wy;
                float y1 = y0 + 1f;
                Vector3 p0 = new Vector3(cx + s.E0.x, y0, cz + s.E0.y);
                Vector3 p1 = new Vector3(cx + s.E0.x, y1, cz + s.E0.y - t0);
                Vector3 p2 = new Vector3(cx + s.E1.x, y1, cz + s.E1.y - t1);
                Vector3 p3 = new Vector3(cx + s.E1.x, y0, cz + s.E1.y);
                if (jitterOn)
                {
                    // Jitter BEFORE the outward z-fight push: the un-pushed positions are bitwise
                    // those of the base wall face, so the overlay inherits the face's exact
                    // displacement and stays glued (the push would drift stacked overlays out of
                    // the frac(z)≈0.1 exemption window and snap them to the wrong corner).
                    p0 = JitterVert(p0, true);
                    p1 = JitterVert(p1, true);
                    p2 = JitterVert(p2, true);
                    p3 = JitterVert(p3, true);
                }

                bool back1 = IsBackFacing(p0, p1, p2);
                bool back2 = IsBackFacing(p0, p2, p3);
                if (back1 && back2)
                {
                    continue; // rides a face the game's Cull Back removes — see IsBackFacing
                }

                p0 += n;
                p1 += n;
                p2 += n;
                p3 += n;
                if (!back1 && !back2)
                {
                    AddQuad(v, uv, cols, t, p0, p1, p2, p3, sprite, Color.white, Color.white, Color.white, Color.white);
                }
                else if (!back1)
                {
                    // per-triangle culling, matching the base face's emission — see AddSide
                    AddTri(v, uv, cols, t, p0, p1, p2,
                        new Vector2(sprite.xMin, sprite.yMin), new Vector2(sprite.xMin, sprite.yMax), new Vector2(sprite.xMax, sprite.yMax),
                        Color.white, Color.white, Color.white);
                }
                else
                {
                    AddTri(v, uv, cols, t, p0, p2, p3,
                        new Vector2(sprite.xMin, sprite.yMin), new Vector2(sprite.xMax, sprite.yMax), new Vector2(sprite.xMax, sprite.yMin),
                        Color.white, Color.white, Color.white);
                }
            }
        }

        // Whether the cell at (x,z) carries the same state on the same base — for adaptive state tiling.
        private bool SameState(int x, int z, LayerName state, bool isWall)
        {
            if (!cells.TryGetValue(new Vector2Int(x, z), out Cell c))
            {
                return false;
            }

            HashSet<LayerName> set = isWall ? c.WallStates : c.GroundStates;
            bool baseOk = isWall ? c.Wall : c.Ground && !c.Wall;
            return baseOk && set != null && set.Contains(state);
        }

        private int StateMask(int cx, int cz, LayerName state, bool isWall)
        {
            int f = 0;
            if (SameState(cx + 1, cz, state, isWall)) f |= DirE;
            if (SameState(cx + 1, cz - 1, state, isWall)) f |= DirSE;
            if (SameState(cx, cz - 1, state, isWall)) f |= DirS;
            if (SameState(cx - 1, cz - 1, state, isWall)) f |= DirSW;
            if (SameState(cx - 1, cz, state, isWall)) f |= DirW;
            if (SameState(cx - 1, cz + 1, state, isWall)) f |= DirNW;
            if (SameState(cx, cz + 1, state, isWall)) f |= DirN;
            if (SameState(cx + 1, cz + 1, state, isWall)) f |= DirNE;
            return f;
        }

        // The above-ground wall side sprite (wallFront layer, adaptive on E/W wall neighbours).
        private static Rect WallFrontUV(int mask, int rnd)
        {
            return DimensionTilesetAtlas.TryGetAdaptiveUV(LayerName.wallFront, mask, rnd, out Rect uv) ? uv : WallTile(rnd);
        }

        // The underground cross-section sprite (groundFront layer, six random variants over three tiles).
        private static Rect GroundFrontUV(int rnd)
        {
            return DimensionTilesetAtlas.TryGetAdaptiveUV(LayerName.groundFront, 0, rnd, out Rect uv) ? uv : GroundTile(rnd);
        }

        private static Mesh BuildMesh(List<Vector3> verts, List<Vector2> uvs, List<Color> cols, List<int> tris)
        {
            Mesh mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTop(
            List<Vector3> verts, List<Vector2> uvs, List<Color> cols, List<int> tris,
            int cx, int cz, float y, Rect uv, float shade,
            float swT = 0f, float nwT = 0f, float neT = 0f, float seT = 0f, bool jitterVerts = false)
        {
            // The trims shift a corner toward -z only; the sprite keeps its full UVs and squashes,
            // exactly as _BuildMeshSingle does (geometry moves, UV rect untouched).
            Vector3 sw = new Vector3(cx - 0.5f, y, cz - 0.5f - swT);
            Vector3 nw = new Vector3(cx - 0.5f, y, cz + 0.5f - nwT);
            Vector3 ne = new Vector3(cx + 0.5f, y, cz + 0.5f - neT);
            Vector3 se = new Vector3(cx + 0.5f, y, cz - 0.5f - seT);
            if (jitterVerts)
            {
                // Caps sample at their own (x, z) — the Ground/TopWall rule; trimmed corners
                // (z = k+0.1) sample their post-trim spot, exactly as the game's shader sees them.
                sw = JitterVert(sw, false);
                nw = JitterVert(nw, false);
                ne = JitterVert(ne, false);
                se = JitterVert(se, false);
            }

            Color col = new Color(shade, shade, shade, 1f);
            AddQuad(verts, uvs, cols, tris, sw, nw, ne, se, uv, col, col, col, col);
        }

        private static void AddSide(
            List<Vector3> verts, List<Vector2> uvs, List<Color> cols, List<int> tris,
            int cx, int cz, Side s, float yLo, float yHi, Rect uv, float shadeLo, float shadeHi,
            float e0TopTrim = 0f, float e1TopTrim = 0f, bool jitterVerts = false)
        {
            // Wall side faces follow the cap's trimmed corners at their TOP edge only — bottom
            // corners never move (see WallCapCornerTrims) — so the face shears toward -z where the
            // cap is trimmed/extended and stays stitched to it, as _BuildMeshSingle's FRONT/BACK/
            // LEFT/RIGHT branches do.
            Vector3 a = new Vector3(cx + s.E0.x, yLo, cz + s.E0.y);
            Vector3 b = new Vector3(cx + s.E0.x, yHi, cz + s.E0.y - e0TopTrim);
            Vector3 c = new Vector3(cx + s.E1.x, yHi, cz + s.E1.y - e1TopTrim);
            Vector3 d = new Vector3(cx + s.E1.x, yLo, cz + s.E1.y);
            if (jitterVerts)
            {
                // Side faces are Amplify/Wall geometry: the sample point snaps to the owning
                // half-integer column corner (an identity for these on-grid verts, keeping them
                // bitwise-sealed to the caps; the frac(z)≈0.1 window lets 0.4-trimmed top verts
                // sample their own spot, stitched to the cap's trimmed corner).
                a = JitterVert(a, true);
                b = JitterVert(b, true);
                c = JitterVert(c, true);
                d = JitterVert(d, true);
            }

            bool back1 = IsBackFacing(a, b, c);
            bool back2 = IsBackFacing(a, c, d);
            if (back1 && back2)
            {
                return; // fully back-facing — the game's Cull Back drops both triangles
            }

            Color lo = new Color(shadeLo, shadeLo, shadeLo, 1f);
            Color hi = new Color(shadeHi, shadeHi, shadeHi, 1f);
            if (!back1 && !back2)
            {
                AddQuad(verts, uvs, cols, tris, a, b, c, d, uv, lo, hi, hi, lo);
                return;
            }

            // MIXED winding (a trim-sheared corner face twisted by jitter — the trimmed top corner
            // sits at z = k+0.1, inside the jitter's exemption window, so it samples its own spot
            // and can twist against the rest of the face). GPU culling is PER TRIANGLE, so emit
            // only the front-facing half; the back-facing half is exactly the sliver the game's
            // Cull Back never draws.
            if (!back1)
            {
                AddTri(verts, uvs, cols, tris, a, b, c,
                    new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax),
                    lo, hi, hi);
            }
            else
            {
                AddTri(verts, uvs, cols, tris, a, c, d,
                    new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin),
                    lo, hi, lo);
            }
        }

        private static void AddTri(
            List<Vector3> verts, List<Vector2> uvs, List<Color> cols, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Color ca, Color cb, Color cc)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc);
            cols.Add(ca); cols.Add(cb); cols.Add(cc);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        // Replicates the game's BACKFACE CULLING for side faces, which our UGC dummy shaders
        // (explicitly "Cull Off") do not do. Why it matters: under jitter, an east/west face's two
        // z-end columns sample different noise spots (the wall rule snaps each corner to its own
        // half-integer column), so the face twists — and the twist direction that would smear its
        // sprite ACROSS THE CAP (brown streaks inside the cap's dark interior at N-S tile
        // junctions, the reported artifact) projects with BACK-FACE winding. The game never draws
        // that side: its tilemap shaders cull back faces (Unity's default; the shipped
        // Amplify_Wall/TopWall sources declare no Cull override), and its vertex order in
        // PugMapLayer2._BuildMeshSingle (FRONT: west-bottom→west-top→east-top→east-bottom;
        // LEFT/RIGHT z-reversed for outward winding) matches AddSide's exactly — verified by a
        // pixel-exact offline sim (scratchpad diagseam.js): with Cull Off the brown-in-black
        // streaks reproduce; with this cull they vanish while the benign over-ground notch (the
        // authentic bend look) stays.
        //
        // Facing test in screen space: with u = x and v = y + z (the 45°+skew projection up to
        // positive scale factors), front-facing = NEGATIVE cross((b−a),(c−a)) — calibrated on the
        // always-visible south face (cross = −1·faceWidth). Untwisted east/west faces give
        // cross = 0 (edge-on, zero pixels) and are skipped either way. Mixed-winding quads
        // (trim-sheared corner faces twisted by jitter) are split and culled PER TRIANGLE by the
        // callers — GPU culling semantics exactly.
        private static bool IsBackFacing(Vector3 a, Vector3 b, Vector3 c)
        {
            float abu = b.x - a.x;
            float abv = (b.y + b.z) - (a.y + a.z);
            float acu = c.x - a.x;
            float acv = (c.y + c.z) - (a.y + a.z);
            return abu * acv - abv * acu >= 0f;
        }

        private static void AddQuad(
            List<Vector3> verts, List<Vector2> uvs, List<Color> cols, List<int> tris,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect uv, Color ca, Color cb, Color cc, Color cd)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(new Vector2(uv.xMin, uv.yMin));
            uvs.Add(new Vector2(uv.xMin, uv.yMax));
            uvs.Add(new Vector2(uv.xMax, uv.yMax));
            uvs.Add(new Vector2(uv.xMax, uv.yMin));
            cols.Add(ca); cols.Add(cb); cols.Add(cc); cols.Add(cd);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        // ---- CK vertex jitter (Phase 1 — the game's "cozy crookedness") ----
        //
        // Port of the game's shader-side deterministic vertex displacement, per the
        // decompile-verified recipe (memory: ck-presentation-recipes-full.txt, jitterRecipe —
        // extracted from the compiled Amplify/Ground, Amplify/Wall, Amplify/TopWall vertex
        // shaders). Per vertex, world units (1 tile = 1.0 = 16 px):
        //
        //   n    = 0.5 * snoise2D((sx, sz) * 0.5)             // Ashima 2D simplex, n ≈ [-0.5, 0.5]
        //   offX = clamp(trunc(n * 16) / 16, -0.125, +0.125)  // whole-PIXEL steps: -2..+2 px
        //   offY = 0
        //   offZ = clamp(n, -0.125, +0.125)                   // smooth sub-pixel, same n
        //
        // offX's 1/16-tile steps are whole INTERNAL-RT pixels — literally, because the preview's
        // RT is locked to the game's 16 px/tile (see the SEAM FIX #2 comment at PixelsPerTile).
        // That lock is what keeps the jitter's side-face slivers art-pixel-sized notches instead
        // of sub-art-pixel hairlines; offZ stays smooth exactly as the game leaves it.
        //
        // ELIGIBILITY: every rendered surface jitters — ground caps, WALL CAPS, side faces, state
        // overlays (in-game confirmed: caps move with their wall columns; the cap-freezing detour
        // is documented at AddCap's cap-jitter note). The per-material amplitude scalar is 1 for
        // the standard tileset materials; only crystal-family tilesets shrink it (0.5/0.92), which
        // the preview does not model.
        //
        // Ground and cap vertices sample at their own (x, z) — the Ground/TopWall shaders use the
        // vertex position directly. Wall SIDE-face vertices (the Amplify/Wall shader) snap their
        // sample point to the owning half-integer column corner via round-half-even, so a wall
        // column bends as one unit:
        //
        //   sx = round_half_even(x + 0.5) - 0.5
        //   zc = frac(z) in [0.09, 0.11] ? 0.9 : 0.5
        //   sz = round_half_even(z + zc) - zc
        //
        // The frac(z)≈0.1 exemption window DOES apply to this preview: the 0.4 wall-top trims put
        // trimmed side-face top vertices at exactly z = k + 0.1, and the 0.9 branch maps every z
        // in the window to k + 0.1 — i.e. those verts keep sampling their own spot, the same spot
        // the (unsnapped) cap corner samples, which is what keeps sheared side tops stitched to
        // the trimmed cap. We implement the in-window branch as the identity it computes, because
        // the float round-trip round_even(z + 0.9) - 0.9 can drift 1 ULP off the raw z (verified
        // offline: 1 of 134 trimmed corners), and sampling the bitwise-identical position is what
        // guarantees offX's whole-pixel quantization can never flip across the cap/side seam.
        //
        // Because every vertex sharing an (x, z) corner across cap/side/state meshes samples the
        // same point, they get the same n and seams never crack (verified bitwise for all on-grid
        // corners). The pattern is 100% deterministic from the scene's absolute tile coordinates —
        // no seed, no time — matching the game's render-origo-anchored worldPos input. (Only the
        // hybrid fallback's four-quarter caps can hairline against a side face's straight top edge
        // at their extra mid-edge verts — geometry the game never emits; the baked-GEN path, one
        // quad per tile like the game, is exactly sealed.)
        //
        // The IgnoreVertexOffsets footprint mask, hit-wobble and hive bob are gameplay systems the
        // preview has no equivalents for; multiplier is the standard 1.0.

        /// <summary>Displaces one built vertex by the game's jitter rule. wallRule = the
        /// Amplify/Wall column-corner snap (side faces); false = ground/cap layers.</summary>
        private static Vector3 JitterVert(Vector3 p, bool wallRule)
        {
            float sx = p.x;
            float sz = p.z;
            if (wallRule)
            {
                sx = RoundHalfEven(p.x + 0.5f) - 0.5f;
                float fz = Frac(p.z);
                if (!(fz >= 0.09f && fz <= 0.11f)) // in-window: identity (see block comment)
                {
                    sz = RoundHalfEven(p.z + 0.5f) - 0.5f;
                }
            }

            float n = 0.5f * Snoise2D(sx * 0.5f, sz * 0.5f);
            float offX = Mathf.Clamp((float)System.Math.Truncate(n * 16f) / 16f, -0.125f, 0.125f);
            float offZ = Mathf.Clamp(n, -0.125f, 0.125f);
            return new Vector3(p.x + offX, p.y, p.z + offZ);
        }
    }
}
