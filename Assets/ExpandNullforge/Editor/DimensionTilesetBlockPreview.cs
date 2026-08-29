using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// A Core-Keeper-faithful 2.5D tileset preview. CK's world camera is orthographic and pitched a
    /// fixed 45° (<c>CameraManager.cs:145</c>: <c>eulerAngles = (45,0,0)</c>) with no rotation — and the
    /// render pipeline then cancels the tilt's vertical foreshortening: PugRP divides the projection's
    /// vertical scale by cos(outputSkewAngle=45°) (<c>PugRP.SetCameraMatrices</c>, PugRP.cs:801-805;
    /// <c>PugCamera.outputSkewAngle = 45f</c>, PugCamera.cs:351-355). Net screen mapping: u = 16px·x,
    /// v = 16px·(y + z) — ground tiles project SQUARE, a wall's 1-unit front face shows its 16px art
    /// 1:1, and "16px = 1 tile" holds on both axes. This preview applies the same stretched projection
    /// at the game's FIXED camera angle — no rotation, ever: orbiting exposed back faces the game can
    /// never render and repeatedly read as bugs. Dragging pans; scrolling zooms; that's the whole
    /// camera. Geometry is extruded like the game's walls: ground is a slab
    /// with depth, walls stand a tile tall on top of ground with cap + side faces. Caps sample the asset's
    /// own BAKED GEN sheets (<see cref="DimensionTilesetAsset.GeneratedGen"/>) — one full 16px tile per
    /// neighbour mask in Core Keeper's canonical packing, exactly what the game renders — falling back to
    /// the captured hybrid lookup (<see cref="DimensionTilesetAtlas"/> STD sprite / four sub-corner
    /// quarters) only for layers not generated yet. Two more pieces of the game's presentation are
    /// emulated: the deterministic simplex vertex jitter that bends wall runs and lifts ground toward
    /// walls (the "cozy crookedness", toggleable), and the internal render target locked to the
    /// game's 16 px-per-tile art density with a point-filtered upscale (the crisp pixelation —
    /// zooming scales the upscale, never the art density, exactly like blowing up a CK screenshot).
    /// Each cell can hold a
    /// ground and, on top of it, a wall; the Edit tool places (left) and removes (right) with CK-style
    /// blue/red placement holograms, and View lets you select a block to toggle its states.
    /// </summary>
    internal sealed class DimensionTilesetBlockPreview
    {
        private enum Tool { View, Edit }

        public enum BlockKind { Ground, Wall }

        // A toggleable block state. Ground states overlay the ground cap; wall states overlay the wall
        // SIDE faces via their "…Front" layer (SideLayer), exactly as the game paints ore/cracks/grass
        // onto the visible wall face rather than its top.
        public struct StateEntry
        {
            public LayerName Layer;
            public string Label;
            public bool IsWall;
            public LayerName SideLayer;
        }

        // The states the studio panel offers for a selected block, curated from the dirt tileset's layers.
        public static readonly StateEntry[] StateCatalog =
        {
            new StateEntry { Layer = LayerName.dugUpGround, Label = "Tilled", IsWall = false },
            new StateEntry { Layer = LayerName.wateredGround, Label = "Watered", IsWall = false },
            new StateEntry { Layer = LayerName.water, Label = "Flooded", IsWall = false },
            new StateEntry { Layer = LayerName.groundSlime, Label = "Slime", IsWall = false },
            new StateEntry { Layer = LayerName.smallStones, Label = "Pebbles", IsWall = false },
            new StateEntry { Layer = LayerName.smallGrass, Label = "Grass", IsWall = false },
            new StateEntry { Layer = LayerName.bigRoot, Label = "Roots", IsWall = false },
            new StateEntry { Layer = LayerName.debris, Label = "Debris", IsWall = false },
            new StateEntry { Layer = LayerName.chrysalis, Label = "Chrysalis", IsWall = false },
            new StateEntry { Layer = LayerName.wallCrack, Label = "Cracks", IsWall = true, SideLayer = LayerName.wallCrackFront },
            new StateEntry { Layer = LayerName.ore, Label = "Ore", IsWall = true, SideLayer = LayerName.oreFront },
            new StateEntry { Layer = LayerName.wallGrass, Label = "Wall grass", IsWall = true, SideLayer = LayerName.wallGrass },
            new StateEntry { Layer = LayerName.ancientCrystal, Label = "Crystal", IsWall = true, SideLayer = LayerName.ancientCrystalFront },
        };

        private const int GridExtent = 32; // 64x64-ish placeable area, cells span [-32, 32]
        private const int DirE = 1, DirSE = 2, DirS = 4, DirSW = 8, DirW = 16, DirNW = 32, DirN = 64, DirNE = 128;

        private const float PitchLocked = 45f; // CK's exact camera pitch — never changes
        private const float CameraDist = 60f;
        private const float DefaultOrthoSize = 4f;

        // The game's anamorphic output skew: PugRP divides the ortho projection's vertical scale by
        // cos(outputSkewAngle) (PugRP.cs:801-805, "proj[1,1] /= Mathf.Cos(outputSkewAngle)"), with
        // outputSkewAngle defaulting to the camera pitch 45° (PugCamera.cs:351-355). This is what
        // makes CK's ground tiles square on screen and wall fronts pixel-perfect despite the 45°
        // tilt. Without it, y/z extents render at cos45 ≈ 0.707× relative to x — walls running
        // east-west look 29% too thin next to walls running north-south.
        private static readonly float SkewCos = Mathf.Cos(PitchLocked * Mathf.Deg2Rad);

        // Phase 2 — the game's internal pixel grid (ck-presentation recipes, postfxRecipe): every
        // world camera renders into a low-res RT and only that RT reaches the screen; on it,
        // 16 px = 1 tile — the game's fixed art-pixel density (480x270 at zoom 1 shows 16.875
        // tiles vertically). The camera is snapped to this texel grid with a +0.25-texel offset
        // (the game's texelSnapOffset) so geometry keeps a stable texel phase while panning.
        //
        // SEAM FIX #2 (the residual "hairlines between wall blocks"): the preview originally kept
        // the game's fixed RT HEIGHT (270 px) while letting the zoom vary orthoSize freely — so
        // the RT's pixels-per-tile drifted off 16 (33.75 at the default zoom). That renders
        // geometry at SUB-ART-PIXEL resolution, and the jitter then produces features the game
        // cannot: its per-corner offX differences twist the east/west wall side faces into thin
        // slivers of dark wallFront art which — verified by a pixel-exact offline simulation of
        // this whole pipeline (scratchpad diagseam.js, tag-buffer provenance) — rasterize 1 RT px
        // wide while every art pixel spans 2.1 RT px: a crack thinner than any art pixel, reading
        // as a rendering artifact ("faint dark hairlines"). The same simulation at exactly
        // 16 px/tile shows the identical geometry as chunky art-integrated notches — the game's
        // own cozy-crooked look (in the game offX is quantized to whole INTERNAL pixels
        // by construction: trunc(n·16)/16 tile = 1 internal px at its fixed 16 px/tile).
        // So the preview now locks the RT to 16 px/tile and derives the RT SIZE from the zoom
        // instead (RT height = 32·orthoSize, since vertical px per tile = H/(2·orthoSize)); the
        // point upscale to the GUI rect supplies the magnification, exactly like blowing up a CK
        // screenshot. At orthoSize 8.4375 this reproduces the game's own 480x270-class framing.
        private const int PixelsPerTile = 16;
        private const float TexelSnapOffset = 0.25f;

        // Vertical layout, straight from the captured layer offsets: ground cap y=0, wall cap y=1. The wall
        // is one tile tall as the game renders it; the wall-side sprite tiles up the face (WallTiles high).
        private const int WallTiles = 1;
        private const float GroundCapY = 0f;
        private const float WallCapY = WallTiles; // top of the wall
        private const float GroundFrontBottom = -3f;

        private const float CapShade = 1f;
        private const float FrontShade = 1f; // front sprites carry their above-ground/underground shading in the art

        private static readonly Vector3 DefaultPivot = new Vector3(0f, -0.3f, 0f);

        // The four orthogonal neighbours and the ground-plane edge each shares with this cell.
        private struct Side
        {
            public int Dx;
            public int Dz;
            public Vector2 E0;
            public Vector2 E1;
        }

        private static readonly Side[] Sides =
        {
            new Side { Dx = 1, Dz = 0, E0 = new Vector2(0.5f, -0.5f), E1 = new Vector2(0.5f, 0.5f) },   // east
            new Side { Dx = -1, Dz = 0, E0 = new Vector2(-0.5f, 0.5f), E1 = new Vector2(-0.5f, -0.5f) }, // west
            new Side { Dx = 0, Dz = 1, E0 = new Vector2(0.5f, 0.5f), E1 = new Vector2(-0.5f, 0.5f) },    // north
            new Side { Dx = 0, Dz = -1, E0 = new Vector2(-0.5f, -0.5f), E1 = new Vector2(0.5f, -0.5f) }, // south
        };

        // A cell carries a ground and, sitting on top of it, a wall, plus each layer's set of toggled
        // states. Its sprite variants aren't stored — they're derived from world position, exactly as the
        // game does, so patterned tilesets lay down their intended pieces.
        private sealed class Cell
        {
            public bool Ground;
            public bool Wall;
            public HashSet<LayerName> GroundStates;
            public HashSet<LayerName> WallStates;
        }

        // One draw batch per baked GEN layer: GEN sheets are separate textures (one per layer), so their
        // quads can't ride the main-sheet mesh — each layer gets its own mesh + material pair, rebuilt
        // together with the scene mesh.
        private sealed class GenBatch
        {
            public LayerName Layer;
            public Texture2D Texture;
            public bool Transparent;
            public readonly List<Vector3> Verts = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<Color> Cols = new List<Color>();
            public readonly List<int> Tris = new List<int>();
            public Mesh Mesh;
            public Material Material;
        }

        private readonly Dictionary<Vector2Int, Cell> cells = new Dictionary<Vector2Int, Cell>();
        // The asset's baked GEN sheet per layer (only layers the atlas has a canonical GEN layout for),
        // refreshed every Draw; a change (generate/regenerate/clear) dirties the scene.
        private readonly Dictionary<LayerName, Texture2D> genTextures = new Dictionary<LayerName, Texture2D>();
        private readonly List<GenBatch> genBatches = new List<GenBatch>();
        private int genSignature;
        private int patternShift; // offsets the whole grid's position hash — "shuffle" the pattern coherently
        private Tool tool = Tool.View;
        private BlockKind palette = BlockKind.Wall;
        private float orthoSize = DefaultOrthoSize;
        private Vector3 pivot = DefaultPivot;
        // CK's deterministic vertex jitter, mirrored from the block's own "Rigid surface" switch
        // rather than exposed as its own control: the preview's job is to show what the block will
        // actually look like, and a separate toggle only invited the question of which one is real.
        // Kept as a field because the built meshes bake it in — a change has to dirty the scene.
        private bool jitterOn = true;
        private bool seeded;

        // Whether the canvas paints its own row of controls. True for the panel that has always
        // drawn them; a page that carries those controls in its own design turns it off around the
        // one draw it owns, so the same state is driven from one place either way.
        private bool drawsOwnToolbar = true;

        private Vector2Int hoverCell;
        private bool hoverValid;
        private Vector2 pressPos;
        private int pressButton;
        private bool dragging;

        private Vector2Int selectedCell;
        private bool hasSelection;
        private bool selectedIsWall;

        private PreviewRenderUtility preview;
        private RenderTexture pixelRT; // Phase 2 — the internal low-res target the scene renders into
        private Material blockMaterial;
        private Material overlayMaterial;
        private Material holoMaterial;
        private Mesh sceneMesh;
        private Mesh stateMesh;
        private bool sceneDirty = true;
        private Mesh overlayMesh;
        private Rect lastOverlayUV = new Rect(-1f, -1f, -1f, -1f);
        private Mesh holoMesh;
        private BlockKind holoKind;
        private bool holoValid;
        private bool holoBuilt;
        private Mesh selectionMesh;
        private Texture lastTexture;

        private Rect? overlayUV;
        private bool overlayOnWall;

        // Debug-export state: the live preview instance the studio last drew, and what it drew with.
        private static DimensionTilesetBlockPreview lastDrawn;
        private Rect lastDrawRect;
        private Texture2D lastSheet;

        private GUIStyle bottomFaint;
        private GUIStyle centeredFaint;
        private GUIStyle miniButton;

        // ---- public API ----

        /// <summary>Sets which block the palette adds by default (called once, before the user picks).</summary>
        public void SetPreferredKind(BlockKind kind)
        {
            if (!seeded)
            {
                palette = kind;
            }
        }

        /// <summary>The overlay sprite (sheet UV) to composite for the selected state; null clears it.</summary>
        public void SetActiveOverlay(Rect? uv, bool onWall)
        {
            overlayUV = uv;
            overlayOnWall = onWall;
        }

        /// <summary>True when the user has a block selected (View mode) to edit its states.</summary>
        public bool HasSelection
        {
            get { return hasSelection; }
        }

        /// <summary>Whether the selected block is a wall (else ground).</summary>
        public bool SelectionIsWall
        {
            get { return selectedIsWall; }
        }

        public bool SelectionHasState(LayerName state)
        {
            if (!hasSelection || !cells.TryGetValue(selectedCell, out Cell c))
            {
                return false;
            }

            HashSet<LayerName> set = selectedIsWall ? c.WallStates : c.GroundStates;
            return set != null && set.Contains(state);
        }

        public void ToggleSelectionState(LayerName state, bool on)
        {
            if (!hasSelection || !cells.TryGetValue(selectedCell, out Cell c))
            {
                return;
            }

            if (selectedIsWall)
            {
                c.WallStates = c.WallStates ?? new HashSet<LayerName>();
                if (on)
                {
                    c.WallStates.Add(state);
                }
                else
                {
                    c.WallStates.Remove(state);
                }
            }
            else
            {
                c.GroundStates = c.GroundStates ?? new HashSet<LayerName>();
                if (on)
                {
                    c.GroundStates.Add(state);
                }
                else
                {
                    c.GroundStates.Remove(state);
                }
            }

            sceneDirty = true;
        }

        // ---- driven from outside: the same state the canvas toolbar edits ----

        /// <summary>
        /// Whether the canvas draws the row of controls along its top edge. Off while a page that
        /// carries those controls in its own design is doing the drawing.
        /// </summary>
        internal bool DrawsOwnToolbar
        {
            get { return drawsOwnToolbar; }
            set { drawsOwnToolbar = value; }
        }

        /// <summary>Which block the canvas adds when someone paints in it: ground or wall.</summary>
        internal BlockKind PaintKind
        {
            get { return palette; }
            set { palette = value; }
        }

        /// <summary>True while painting is on; false while clicks select a block instead.</summary>
        internal bool Painting
        {
            get { return tool == Tool.Edit; }
            set { tool = value ? Tool.Edit : Tool.View; }
        }

        /// <summary>
        /// Shifts the whole grid's position hash, so the pattern lands in a different but equally
        /// coherent arrangement. Every tile is still the piece the tileset intended for it.
        /// </summary>
        internal void ShufflePattern()
        {
            patternShift++;
            sceneDirty = true;
        }

        /// <summary>Puts the camera, the zoom and the pattern back where they started.</summary>
        internal void ResetView()
        {
            orthoSize = DefaultOrthoSize;
            pivot = DefaultPivot;
            patternShift = 0;
            sceneDirty = true;
        }

        /// <summary>
        /// Mirrors the block's "Rigid surface" switch into the preview: a rigid block renders straight,
        /// anything else takes Core Keeper's vertex wobble, so what you see here is what the game will
        /// draw. The jitter is baked into the built meshes, so a change has to rebuild the scene.
        /// </summary>
        private void SyncJitter(DimensionTilesetAsset tilesetAsset)
        {
            bool wanted = tilesetAsset == null || !tilesetAsset.RigidSurface;
            if (wanted == jitterOn)
            {
                return;
            }

            jitterOn = wanted;
            sceneDirty = true;
        }

        public void Draw(Rect rect, Texture2D texture, DimensionTilesetAsset tilesetAsset)
        {
            Seed();
            SyncGeneratedGen(tilesetAsset);
            SyncJitter(tilesetAsset);

            // Recorded for the debug export (Dimensions API/Developer/Export Preview Render), which
            // re-renders THIS instance's exact scene state off-screen.
            lastDrawn = this;
            lastDrawRect = rect;
            lastSheet = texture;

            // The strip the canvas keeps for its own controls is only dead to the mouse while those
            // controls are actually there; a page that carries them itself gets the whole canvas.
            Rect topStrip = drawsOwnToolbar
                ? new Rect(rect.x, rect.y, rect.width, 28f)
                : new Rect(rect.x, rect.y, 0f, 0f);
            HandleInput(rect, topStrip);
            ValidateSelection();
            UpdateHover(rect);

            if (texture == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.027f, 0.035f, 0.045f, 1f));
                GUI.Label(rect, "no sheet", CenteredFaint());
                DrawBorder(rect);
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                EnsurePreview();
                EnsureMeshes();
                EnsureMaterials(texture);
                Render(rect);
            }

            DrawBorder(rect);
            if (drawsOwnToolbar)
            {
                DrawToolbar(rect);
            }

            DrawBottomText(rect);
        }

        // Mirror the asset's baked GEN sheets into the layer→texture map (only layers whose canonical
        // GEN layout was captured, so the cell lookup can address them). When the set changes — the
        // modder generated, regenerated or cleared — the scene rebuilds so caps re-resolve.
        private void SyncGeneratedGen(DimensionTilesetAsset tilesetAsset)
        {
            genTextures.Clear();
            int sig = 17;
            if (tilesetAsset != null && tilesetAsset.GeneratedGen != null)
            {
                foreach (DimensionGeneratedGenLayer g in tilesetAsset.GeneratedGen)
                {
                    if (g == null || g.texture == null || !DimensionTilesetAtlas.HasGenLayout(g.layer))
                    {
                        continue;
                    }

                    genTextures[g.layer] = g.texture;
                    sig = sig * 31 + (int)g.layer;
                    sig = sig * 31 + g.texture.GetInstanceID();
                }
            }

            if (sig != genSignature)
            {
                genSignature = sig;
                sceneDirty = true;
            }
        }

        // ---- input ----

        private void HandleInput(Rect rect, Rect topStrip)
        {
            Event e = Event.current;
            int id = GUIUtility.GetControlID(FocusType.Passive);
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if ((e.button == 0 || e.button == 1 || e.button == 2) && rect.Contains(e.mousePosition) && !topStrip.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        pressPos = e.mousePosition;
                        pressButton = e.button;
                        dragging = false;
                        e.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        if (!dragging && (e.mousePosition - pressPos).sqrMagnitude > 12f)
                        {
                            dragging = true;
                        }

                        if (dragging)
                        {
                            // Camera never rotates — the game's doesn't, and orbiting exposed faces
                            // the game can't render (a repeated source of false bug reports). Any
                            // drag pans; zoom/scroll unchanged.
                            if (pressButton == 2 || pressButton == 0)
                            {
                                Pan(rect, e.delta);
                            }
                        }

                        e.Use();
                    }

                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        if (!dragging && PickCell(rect, e.mousePosition, out Vector2Int cell))
                        {
                            if (tool == Tool.Edit && pressButton == 0)
                            {
                                TryPlace(cell);
                            }
                            else if (tool == Tool.Edit && pressButton == 1)
                            {
                                TryRemove(cell);
                            }
                            else if (tool == Tool.View && pressButton == 0)
                            {
                                Select(cell);
                            }
                        }

                        GUIUtility.hotControl = 0;
                        e.Use();
                    }

                    break;
                case EventType.ScrollWheel:
                    if (rect.Contains(e.mousePosition))
                    {
                        orthoSize = Mathf.Clamp(orthoSize + e.delta.y * 0.25f, 1.5f, 22f);
                        e.Use();
                    }

                    break;
            }
        }

        // Slide the pivot across the ground plane along the camera's screen axes.
        private void Pan(Rect rect, Vector2 delta)
        {
            Quaternion rot = Quaternion.Euler(PitchLocked, 0f, 0f);
            Vector3 right = rot * Vector3.right;
            Vector3 fwd = rot * Vector3.forward;
            right.y = 0f;
            fwd.y = 0f;
            right = right.sqrMagnitude > 1e-4f ? right.normalized : Vector3.right;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            float worldPerPixel = 2f * orthoSize / Mathf.Max(1f, rect.height);
            pivot -= right * (delta.x * worldPerPixel);
            pivot += fwd * (delta.y * worldPerPixel);
        }

        private void UpdateHover(Rect rect)
        {
            hoverValid = false;
            if (tool != Tool.Edit || dragging || !PickCell(rect, Event.current.mousePosition, out Vector2Int cell))
            {
                return;
            }

            hoverCell = cell;
            hoverValid = true;
        }

        // Ray-cast the mouse through the fixed 45° orthographic camera onto the ground plane (Y=0).
        //
        // Pick math under the internal pixel RT, verified analytically: the camera maps a world
        // point to NDC u = dot(P-camPos, right)/(orthoSize·aspect), v = dot(P-camPos, up)/
        // (orthoSize·cos45) (m11 = 1/(orthoSize·cos45) after the skew divide). That NDC lands at
        // RT pixel ((u+1)/2·W, (v+1)/2·H), and the blit maps the RT's full extent onto BlitRect —
        // so mouse → NDC is a rect-fraction over the BLIT rect (identical to the old whole-rect
        // math when the blit fills the rect). Clicks in the letterbox borders pick nothing. The
        // camera renders with the INTERNAL aspect W/H and a texel-snapped position; both are
        // reproduced here so picking inverts exactly what Render() drew.
        private bool PickCell(Rect rect, Vector2 mouse, out Vector2Int cell)
        {
            cell = default;
            if (rect.width < 1f || rect.height < 1f || !rect.Contains(mouse))
            {
                return false;
            }

            InternalResolution(rect, out int rtW, out int rtH);
            Rect blit = BlitRect(rect, rtW, rtH);
            if (!blit.Contains(mouse))
            {
                return false;
            }

            float u = (mouse.x - blit.x) / blit.width * 2f - 1f;
            float ny = (1f - (mouse.y - blit.y) / blit.height) * 2f - 1f;
            float aspect = rtW / (float)rtH;

            Quaternion rot = Quaternion.Euler(PitchLocked, 0f, 0f);
            Vector3 right = rot * Vector3.right;
            Vector3 up = rot * Vector3.up;
            Vector3 fwd = rot * Vector3.forward;
            Vector3 camPos = SnappedCameraPosition(rot, rtW, rtH);
            // With the outputSkew stretch, one NDC unit vertically spans orthoSize*cos45 world units
            // along the camera-up axis (the stretch shows fewer world units per screen height).
            Vector3 origin = camPos + right * (u * orthoSize * aspect) + up * (ny * orthoSize * SkewCos);
            if (Mathf.Abs(fwd.y) < 1e-4f)
            {
                return false;
            }

            float t = -origin.y / fwd.y;
            Vector3 world = origin + fwd * t;
            int cx = Mathf.RoundToInt(world.x);
            int cz = Mathf.RoundToInt(world.z);
            if (cx < -GridExtent || cx > GridExtent || cz < -GridExtent || cz > GridExtent)
            {
                return false;
            }

            cell = new Vector2Int(cx, cz);
            return true;
        }

        // ---- placement model ----

        private bool HasGround(int x, int z)
        {
            return cells.TryGetValue(new Vector2Int(x, z), out Cell c) && c.Ground;
        }

        private bool HasWall(int x, int z)
        {
            return cells.TryGetValue(new Vector2Int(x, z), out Cell c) && c.Wall;
        }

        private bool Same(int x, int z, BlockKind kind)
        {
            // Ground-cap adaptivity uses VISIBLE ground only: a ground covered by a wall is hidden, so the
            // exposed ground beside it must show its rim, not connect toward a surface you can't see. Bury
            // the wall and the ground under it stops counting; break the wall later and it connects again.
            return kind == BlockKind.Wall ? HasWall(x, z) : HasGround(x, z) && !HasWall(x, z);
        }

        // Ground places only where there's no ground yet; a wall needs a ground under it and no wall yet.
        private bool CanPlace(Vector2Int cell)
        {
            cells.TryGetValue(cell, out Cell c);
            if (palette == BlockKind.Ground)
            {
                return c == null || !c.Ground;
            }

            return c != null && c.Ground && !c.Wall;
        }

        private void TryPlace(Vector2Int cell)
        {
            if (!CanPlace(cell))
            {
                return;
            }

            if (!cells.TryGetValue(cell, out Cell c))
            {
                c = new Cell();
                cells[cell] = c;
            }

            if (palette == BlockKind.Ground)
            {
                c.Ground = true;
            }
            else
            {
                c.Wall = true;
            }

            sceneDirty = true;
        }

        // Right-click peels the top layer: the wall first (keeping the ground), then the ground.
        private void TryRemove(Vector2Int cell)
        {
            if (!cells.TryGetValue(cell, out Cell c))
            {
                return;
            }

            if (c.Wall)
            {
                c.Wall = false;
                c.WallStates = null;
            }
            else
            {
                cells.Remove(cell);
            }

            sceneDirty = true;
        }

        private void Select(Vector2Int cell)
        {
            if (!cells.TryGetValue(cell, out Cell c))
            {
                hasSelection = false;
                return;
            }

            hasSelection = true;
            selectedCell = cell;
            selectedIsWall = c.Wall; // the top layer under the cursor
        }

        private void ValidateSelection()
        {
            if (!hasSelection)
            {
                return;
            }

            if (!cells.TryGetValue(selectedCell, out Cell c) || (selectedIsWall ? !c.Wall : !c.Ground))
            {
                hasSelection = false;
            }
        }

        // ---- render ----

        private void Render(Rect rect)
        {
            if (rect.width < 1f || rect.height < 1f)
            {
                return;
            }

            RenderSceneToRT(rect);
            InternalResolution(rect, out int rtW, out int rtH);
            DrawBlit(rect, rtW, rtH);
        }

        // Renders the scene into the internal pixel RT — everything except the GUI blit, so the
        // debug export can capture the exact pixels Unity produces without a GUI context.
        private void RenderSceneToRT(Rect rect)
        {
            // Phase 2 — pixel-perfect chain (postfxRecipe): the scene renders into the internal
            // low-res RT (16 px/tile; H from the zoom, W from the rect's aspect), which is then
            // blitted over the GUI rect with POINT filtering — CK's crisp chunky pixels.
            // HDR/bloom/lighting are later phases; the flat-lit look is unchanged, just pixelated.
            InternalResolution(rect, out int rtW, out int rtH);
            EnsurePixelRT(rtW, rtH);

            Camera cam = preview.camera;
            Quaternion rot = Quaternion.Euler(PitchLocked, 0f, 0f);
            cam.transform.rotation = rot;
            // Texel-snapped camera (+0.25 texel, the game's texelSnapOffset) — see SnappedCameraPosition.
            cam.transform.position = SnappedCameraPosition(rot, rtW, rtH);
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.aspect = rtW / (float)rtH; // the INTERNAL aspect — the blit stretches it over rect
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = CameraDist * 2f + 40f;
            cam.clearFlags = CameraClearFlags.Color;
            // Pixel-art renders with NO anti-aliasing — MSAA would blend dark cap pixels with
            // bright rim pixels along jittered junction edges, painting exactly the stray brown
            // lines this preview must never invent. PreviewRenderUtility's camera allows MSAA by
            // default; the game's does not.
            cam.allowMSAA = false;
            // The same near-black the Portal Studio floor uses: the two previews are rooms in
            // one building, and a block's own colours read truest against the dark the game
            // actually shows around them.
            cam.backgroundColor = new Color(0.027f, 0.035f, 0.045f, 1f);
            cam.targetTexture = pixelRT;

            // The game's outputSkew (PugRP.cs:801-805): stretch the projection's vertical scale by
            // 1/cos(45°) so screen v = y + z at equal scale with x — CK's actual on-screen metric.
            cam.ResetProjectionMatrix();
            Matrix4x4 proj = cam.projectionMatrix;
            proj.m11 /= SkewCos;
            cam.projectionMatrix = proj;

            preview.DrawMesh(sceneMesh, Matrix4x4.identity, blockMaterial, 0);
            for (int i = 0; i < genBatches.Count; i++)
            {
                GenBatch b = genBatches[i];
                if (!b.Transparent && b.Mesh != null)
                {
                    preview.DrawMesh(b.Mesh, Matrix4x4.identity, b.Material, 0); // baked GEN caps (opaque)
                }
            }

            preview.DrawMesh(stateMesh, Matrix4x4.identity, overlayMaterial, 0); // per-block state overlays
            for (int i = 0; i < genBatches.Count; i++)
            {
                GenBatch b = genBatches[i];
                if (b.Transparent && b.Mesh != null)
                {
                    preview.DrawMesh(b.Mesh, Matrix4x4.identity, b.Material, 0); // baked GEN state overlays
                }
            }

            if (overlayUV.HasValue)
            {
                EnsureOverlayMesh(overlayUV.Value);
                foreach (KeyValuePair<Vector2Int, Cell> kv in cells)
                {
                    Cell c = kv.Value;
                    bool draw = overlayOnWall ? c.Wall : c.Ground && !c.Wall;
                    if (draw)
                    {
                        float y = (overlayOnWall ? WallCapY : 0f) + 0.02f;
                        preview.DrawMesh(overlayMesh, Matrix4x4.Translate(new Vector3(kv.Key.x, y, kv.Key.y)), overlayMaterial, 0);
                    }
                }
            }

            if (tool == Tool.Edit && hoverValid)
            {
                EnsureHoloMesh(palette, CanPlace(hoverCell));
                Matrix4x4 m = Matrix4x4.TRS(new Vector3(hoverCell.x, 0f, hoverCell.y), Quaternion.identity, new Vector3(1.02f, 1.02f, 1.02f));
                preview.DrawMesh(holoMesh, m, holoMaterial, 0);
            }

            if (hasSelection)
            {
                EnsureSelectionMesh();
                Matrix4x4 m = Matrix4x4.TRS(new Vector3(selectedCell.x, 0f, selectedCell.y), Quaternion.identity, new Vector3(1.05f, 1.05f, 1.05f));
                preview.DrawMesh(selectionMesh, m, holoMaterial, 0);
            }

            // During an export, draw the RENDERED calibration strip: known vertex colors through
            // the real preview material into the RT's top-right corner. The copied strip (see
            // ExportRT) measures only the readback path; this one measures the RENDER path —
            // comparing the two against CalibrationRow separates sample/write/readback transforms
            // exactly instead of inferring them.
            if (exportProbeActive)
            {
                DrawRenderedCalibration(cam, rtW, rtH);
            }

            // Render with the scriptable render pipeline BYPASSED — the mechanical verdict on the
            // persistent "brown marks at cap junctions": calling cam.Render() directly sends the
            // preview camera through the project's ACTIVE SRP (this SDK ships PugRP), whose
            // tonemap + resolve chain both shifts every color (flat art regions came back
            // remapped, background lifted +68) and BLENDS art-pixel edges (a pixel-exact offline
            // reproduction of this exact exported scene matched a raw render everywhere except
            // tile-boundary pixels, where the export held 76 blend colors our 14-color palette
            // cannot produce — the brown marks). PreviewRenderUtility.Render() avoids this with
            // the same switch; we call cam.Render() ourselves, so we flip it ourselves.
            //
            // GL.sRGBWrite: in a Linear project the sRGB-flagged RT only gamma-encodes shader
            // output while this state is ON; editor GUI code commonly leaves it OFF, which writes
            // linear values raw into the sRGB RT — geometry comes out darkened/shifted downstream
            // while raw copies stay byte-perfect. Record the incoming state for the probe, force
            // it on for the render, restore after.
            bool prevSrgbWrite = GL.sRGBWrite;
            lastRenderStateProbe = "GL.sRGBWrite(before render)=" + prevSrgbWrite;
            bool prevSrp = Unsupported.useScriptableRenderPipeline;
            Unsupported.useScriptableRenderPipeline = false;
            GL.sRGBWrite = true;
            // SCENE FOG is inherited from the OPEN Unity scene's RenderSettings and is baked into
            // the shaders via fog macros: it blends GEOMETRY toward the fog color BY DEPTH — a
            // per-pixel-varying, geometry-only tint that survives the SRP bypass. That is exactly
            // the measured contamination signature (no uniform transform fit; background raw;
            // ground shifted more than near wall faces). The preview must render fog-free.
            bool prevFog = RenderSettings.fog;
            RenderSettings.fog = false;
            try
            {
                cam.Render();
            }
            finally
            {
                RenderSettings.fog = prevFog;
                GL.sRGBWrite = prevSrgbWrite;
                Unsupported.useScriptableRenderPipeline = prevSrp;
                cam.targetTexture = null;
            }
        }

        private static bool exportProbeActive;
        private static string lastRenderStateProbe;
        private Mesh calibStripMesh;
        private Material calibMaterial;

        // Eight 1px-wide swatches with CalibrationRow vertex colors, world-positioned to land on
        // RT pixel columns [W-10 .. W-3], rows [0..3) at the top edge — white texture, so the
        // fragment output is exactly the vertex color and the readback measures the WRITE stage.
        private void DrawRenderedCalibration(Camera cam, int rtW, int rtH)
        {
            if (calibMaterial == null)
            {
                calibMaterial = new Material(FindShader()) { hideFlags = HideFlags.HideAndDontSave };
                calibMaterial.mainTexture = Texture2D.whiteTexture;
            }

            DestroyMesh(ref calibStripMesh);
            List<Vector3> v = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<Color> c = new List<Color>();
            List<int> t = new List<int>();
            Vector3 camPos = cam.transform.position;
            Vector3 right = cam.transform.right;
            Vector3 up = cam.transform.up;
            Vector3 fwd = cam.transform.forward;
            float halfW = orthoSize * (rtW / (float)rtH);
            float halfH = orthoSize * SkewCos; // vertical NDC unit spans orthoSize·cos45 world units
            for (int i = 0; i < CalibrationRow.Length; i++)
            {
                float u0 = ((rtW - 10 + i) / (float)rtW) * 2f - 1f;
                float u1 = ((rtW - 9 + i) / (float)rtW) * 2f - 1f;
                float v0 = 1f - 2f * (3f / rtH);
                const float v1 = 1f;
                Color col = CalibrationRow[i];
                Vector3 a = camPos + fwd * 1f + right * (u0 * halfW) + up * (v0 * halfH);
                Vector3 b = camPos + fwd * 1f + right * (u0 * halfW) + up * (v1 * halfH);
                Vector3 cc = camPos + fwd * 1f + right * (u1 * halfW) + up * (v1 * halfH);
                Vector3 d = camPos + fwd * 1f + right * (u1 * halfW) + up * (v0 * halfH);
                AddQuad(v, uv, c, t, a, b, cc, d, new Rect(0.5f, 0.5f, 0f, 0f), col, col, col, col);
            }

            calibStripMesh = BuildMesh(v, uv, c, t);
            preview.DrawMesh(calibStripMesh, Matrix4x4.identity, calibMaterial, 0);
        }

        // The pixelation step — the game's INTEGER-SCALING resolve (postfxRecipe 6: "scale =
        // floor(min(screenW/W, screenH/H)), centered viewport W·scale × H·scale, plain
        // nearest, borders"). A fractional point-stretch duplicates rows/cols unevenly (art
        // pixels 3 px here, 4 px there — visibly warping the 16px art); the integer blit keeps
        // every art pixel uniformly sized, letterboxed with the preview background. Same
        // GUI.DrawTexture call PreviewRenderUtility.EndAndDrawPreview uses, so color-space
        // behavior in the editor GUI is unchanged.
        private void DrawBlit(Rect rect, int rtW, int rtH)
        {
            Rect blit = BlitRect(rect, rtW, rtH);
            if (blit != rect)
            {
                EditorGUI.DrawRect(rect, new Color(0.027f, 0.035f, 0.045f, 1f)); // letterbox borders
            }

            // DrawPreviewTexture is the editor's colorspace-correct path for showing an sRGB RT in
            // GUI (GUI.DrawTexture can double-convert in Linear projects); point filtering still
            // comes from the RT's own filterMode.
            EditorGUI.DrawPreviewTexture(blit, pixelRT, null, ScaleMode.StretchToFill);
        }

        // Where the internal RT lands inside the GUI rect: the largest integer point-upscale that
        // fits, centered (floored to whole GUI pixels so the blit starts on a pixel boundary).
        // Zoomed far out the RT can exceed the rect (scale < 1) — then plain stretch-minify, the
        // game's own aliasing when it can't integer-scale. PickCell inverts this exact mapping.
        private static Rect BlitRect(Rect rect, int rtW, int rtH)
        {
            int scale = Mathf.FloorToInt(Mathf.Min(rect.width / rtW, rect.height / rtH));
            if (scale < 1)
            {
                return rect;
            }

            float w = rtW * scale;
            float h = rtH * scale;
            return new Rect(
                rect.x + Mathf.Floor((rect.width - w) * 0.5f),
                rect.y + Mathf.Floor((rect.height - h) * 0.5f),
                w,
                h);
        }

        // The internal resolution: locked to the game's 16 px/tile art density (see the SEAM FIX #2
        // block comment at PixelsPerTile). Vertical px per tile = H/(2·orthoSize) — the anamorphic
        // skew makes horizontal and vertical px/tile equal whenever the camera aspect matches the
        // RT aspect, which it does — so H = 2·PixelsPerTile·orthoSize pins both to exactly 16.
        // orthoSize only ever moves in 0.25 steps (wheel) from 4.0, so 32·orthoSize is an integer
        // and the lock is exact; RoundToInt guards float dust. W fills the rect's aspect, rounded
        // to even like the game's width rule. A pure function of rect+zoom, so PickCell reproduces
        // it exactly.
        private void InternalResolution(Rect rect, out int w, out int h)
        {
            h = Mathf.Max(PixelsPerTile, Mathf.RoundToInt(orthoSize * 2f * PixelsPerTile));
            float aspect = rect.height >= 1f ? rect.width / rect.height : 1f;
            w = Mathf.CeilToInt(aspect * h);
            if ((w & 1) == 1)
            {
                w++;
            }
        }

        // Camera position snapped to the internal texel grid plus the game's +0.25-texel offset
        // (postfxRecipe "texelSnapOffset 0.25"), along the camera-right and camera-up axes. One
        // horizontal texel spans 2·orthoSize·aspect/W world units along camera-right; one vertical
        // texel spans 2·orthoSize·cos45/H along camera-up (the skewed projection shows
        // orthoSize·cos45 world units per NDC unit vertically). Distance along forward is
        // irrelevant for an ortho camera and stays untouched.
        private Vector3 SnappedCameraPosition(Quaternion rot, int rtW, int rtH)
        {
            Vector3 right = rot * Vector3.right;
            Vector3 up = rot * Vector3.up;
            Vector3 fwd = rot * Vector3.forward;
            Vector3 camPos = pivot - fwd * CameraDist;
            float aspect = rtW / (float)rtH;
            float texelX = 2f * orthoSize * aspect / rtW;
            float texelY = 2f * orthoSize * SkewCos / rtH;
            float rx = Vector3.Dot(camPos, right);
            float uy = Vector3.Dot(camPos, up);
            camPos += right * ((Mathf.Floor(rx / texelX) + TexelSnapOffset) * texelX - rx);
            camPos += up * ((Mathf.Floor(uy / texelY) + TexelSnapOffset) * texelY - uy);
            return camPos;
        }

        private void EnsurePixelRT(int w, int h)
        {
            if (pixelRT != null && (pixelRT.width != w || pixelRT.height != h))
            {
                ReleasePixelRT();
            }

            if (pixelRT == null)
            {
                // LDR for now — the HDR (RGBA16F) upgrade belongs to the later bloom/lighting
                // phases. Explicit sRGB (not Default) so the RT deterministically stores
                // display-encoded bytes in Linear-colorspace projects: the GUI blit shows them
                // as-is and the debug export can write the raw readback without guessing the
                // encoding (in Gamma projects the flag is ignored).
                pixelRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    name = "CKPixelPreviewRT",
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point, // the crisp-pixel upscale
                    antiAliasing = 1, // no MSAA resolve — see cam.allowMSAA note
                };
            }
        }

        private void ReleasePixelRT()
        {
            if (pixelRT != null)
            {
                pixelRT.Release();
                Object.DestroyImmediate(pixelRT);
                pixelRT = null;
            }
        }

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

        // ---- gpu resources ----

        private void EnsurePreview()
        {
            if (preview == null)
            {
                preview = new PreviewRenderUtility();
            }
        }

        private void EnsureMaterials(Texture texture)
        {
            if (blockMaterial == null)
            {
                blockMaterial = new Material(FindShader()) { hideFlags = HideFlags.HideAndDontSave };
            }

            if (overlayMaterial == null)
            {
                overlayMaterial = new Material(FindTransparentShader()) { hideFlags = HideFlags.HideAndDontSave };
            }

            if (holoMaterial == null)
            {
                holoMaterial = new Material(FindTransparentShader()) { hideFlags = HideFlags.HideAndDontSave };
                holoMaterial.mainTexture = Texture2D.whiteTexture;
            }

            if (lastTexture != texture)
            {
                blockMaterial.mainTexture = texture;
                overlayMaterial.mainTexture = texture;
                lastTexture = texture;
            }
        }

        private static Shader FindShader()
        {
            return Shader.Find("UGC_Dummy/Opaque") ?? Shader.Find("SpriteObject/Simple") ?? Shader.Find("Sprites/Default");
        }

        private static Shader FindTransparentShader()
        {
            return Shader.Find("UGC_Dummy/Transparent") ?? Shader.Find("Sprites/Default");
        }

        private void EnsureMeshes()
        {
            if (sceneMesh == null || sceneDirty)
            {
                BuildSceneMesh();
                sceneDirty = false;
            }
        }

        private void EnsureOverlayMesh(Rect uv)
        {
            if (overlayMesh != null && lastOverlayUV == uv)
            {
                return;
            }

            lastOverlayUV = uv;
            DestroyMesh(ref overlayMesh);
            List<Vector3> v = new List<Vector3>();
            List<Vector2> t = new List<Vector2>();
            List<Color> c = new List<Color>();
            List<int> i = new List<int>();
            AddTop(v, t, c, i, 0, 0, 0f, uv, 1f);
            overlayMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            overlayMesh.SetVertices(v);
            overlayMesh.SetUVs(0, t);
            overlayMesh.SetColors(c);
            overlayMesh.SetTriangles(i, 0);
            overlayMesh.RecalculateBounds();
        }

        public void Cleanup()
        {
            if (preview != null)
            {
                preview.Cleanup();
                preview = null;
            }

            ReleasePixelRT();
            DisposeGenBatches();
            DestroyMesh(ref sceneMesh);
            DestroyMesh(ref stateMesh);
            DestroyMesh(ref overlayMesh);
            DestroyMesh(ref holoMesh);
            DestroyMesh(ref selectionMesh);
            DestroyMesh(ref calibStripMesh);
            DestroyMaterial(ref blockMaterial);
            DestroyMaterial(ref overlayMaterial);
            DestroyMaterial(ref holoMaterial);
            DestroyMaterial(ref calibMaterial);
        }

        private static void DestroyMesh(ref Mesh mesh)
        {
            if (mesh != null)
            {
                Object.DestroyImmediate(mesh);
                mesh = null;
            }
        }

        private static void DestroyMaterial(ref Material mat)
        {
            if (mat != null)
            {
                Object.DestroyImmediate(mat);
                mat = null;
            }
        }

        // ---- debug export ----

        /// <summary>
        /// Writes the CURRENT preview scene — rendered through the real pipeline into the internal
        /// pixel RT — to <c>Library/ExpandNullforge/PreviewExport/preview.png</c> plus a 4×
        /// point-upscaled copy, so the actual Unity-rendered pixels can be inspected from disk and
        /// diffed against the offline simulator without screenshotting. Uses the last-drawn
        /// preview instance (the Tileset Studio's, with its live edit state); if none exists yet,
        /// builds the default seeded scene from the selected tileset asset.
        /// </summary>
        // Under Developer, not in the creator's flow — this is a tool for building the framework
        // itself. It sat with no menu item and no caller at all, which made the only written-down
        // way to diff Unity's real render against the offline simulator unreachable; the recipe is
        // written up in Docs/tileset-preview-export-recipe.md and this is what runs it.
        [MenuItem("Dimensions API/Developer/Export Preview Render")]
        internal static void ExportPreviewRender()
        {
            DimensionTilesetBlockPreview p = lastDrawn;
            bool temporary = false;
            if (p == null || p.lastSheet == null)
            {
                DimensionTilesetAsset asset = Selection.activeObject as DimensionTilesetAsset;
                if (asset == null || asset.TilesetTexture == null)
                {
                    Debug.LogWarning(
                        "[Dimensions API] Export Preview Render: open the Tileset Studio first, or select a tileset asset that has a sheet.");
                    return;
                }

                p = new DimensionTilesetBlockPreview();
                temporary = true;
                p.Seed();
                p.SyncGeneratedGen(asset);
                p.lastSheet = asset.TilesetTexture;
                p.lastDrawRect = new Rect(0f, 0f, 580f, 460f);
            }

            try
            {
                Rect rect = p.lastDrawRect.width >= 1f && p.lastDrawRect.height >= 1f
                    ? p.lastDrawRect
                    : new Rect(0f, 0f, 580f, 460f);
                p.EnsurePreview();
                p.EnsureMeshes();
                p.EnsureMaterials(p.lastSheet);
                exportProbeActive = true; // draw the rendered calibration strip this render
                p.RenderSceneToRT(rect);
                ExportRT(p.pixelRT);
                WriteSceneDump(p);
                WriteProbe(p);
            }
            finally
            {
                exportProbeActive = false;
                if (temporary)
                {
                    p.Cleanup();
                }
            }
        }

        private const string ExportDir = "Library/ExpandNullforge/PreviewExport";

        // The scene layout + camera state alongside the pixels, so an offline reproduction never
        // has to reverse-engineer the layout or the pan/zoom from the image again.
        private static void WriteSceneDump(DimensionTilesetBlockPreview p)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("orthoSize=").Append(p.orthoSize.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("pivot=")
                .Append(p.pivot.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(p.pivot.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(p.pivot.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("patternShift=").Append(p.patternShift).Append('\n');
            sb.Append("jitter=").Append(p.jitterOn ? 1 : 0).Append('\n');
            sb.Append("rect=").Append((int)p.lastDrawRect.width).Append('x').Append((int)p.lastDrawRect.height).Append('\n');
            foreach (KeyValuePair<Vector2Int, Cell> kv in p.cells)
            {
                Cell c = kv.Value;
                sb.Append("cell=").Append(kv.Key.x).Append(',').Append(kv.Key.y)
                    .Append(" ground=").Append(c.Ground ? 1 : 0)
                    .Append(" wall=").Append(c.Wall ? 1 : 0);
                if (c.GroundStates != null && c.GroundStates.Count > 0)
                {
                    sb.Append(" gstates=").Append(string.Join("|", c.GroundStates));
                }

                if (c.WallStates != null && c.WallStates.Count > 0)
                {
                    sb.Append(" wstates=").Append(string.Join("|", c.WallStates));
                }

                sb.Append('\n');
            }

            System.IO.File.WriteAllText(ExportDir + "/preview_scene.txt", sb.ToString());
        }

        // The known byte values injected into the RT corner before readback (raw GPU copy, no
        // colorspace math) — whatever transform the readback chain applies shows up on these
        // exactly, ending all guesswork about sRGB conversions in the exported PNG.
        private static readonly Color32[] CalibrationRow =
        {
            new Color32(0, 0, 0, 255), new Color32(32, 32, 32, 255), new Color32(64, 64, 64, 255),
            new Color32(128, 128, 128, 255), new Color32(192, 192, 192, 255), new Color32(255, 255, 255, 255),
            new Color32(255, 0, 0, 255), new Color32(0, 0, 255, 255),
        };

        private static void ExportRT(RenderTexture rt)
        {
            if (rt == null)
            {
                Debug.LogWarning("[Dimensions API] Export Preview Render: nothing was rendered.");
                return;
            }

            // Inject the calibration strip as a RAW byte copy into the RT's (0,0) corner.
            Texture2D calib = new Texture2D(CalibrationRow.Length, 1, TextureFormat.RGBA32, false, true);
            calib.SetPixels32(CalibrationRow);
            calib.Apply(false);
            try
            {
                Graphics.CopyTexture(calib, 0, 0, 0, 0, CalibrationRow.Length, 1, rt, 0, 0, 0, 0);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Dimensions API] calibration copy failed: " + e.Message);
            }

            RenderTexture prevActive = RenderTexture.active;
            // Readback texture colorspace flag MATCHES the RT's — a mismatch makes Unity convert
            // during ReadPixels, which has been corrupting export brightness. The PNG is the RT's
            // raw bytes; the calibration strip in preview_probe.txt states exactly what transform
            // (if any) the readback applied, so the PNG needs no further interpretation guesses.
            Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, !rt.sRGB);
            try
            {
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0f, 0f, rt.width, rt.height), 0, 0);
                tex.Apply(false);
            }
            finally
            {
                RenderTexture.active = prevActive;
            }

            lastCalibrationReadback = ReadCalibration(tex);
            Object.DestroyImmediate(calib);

            System.IO.Directory.CreateDirectory(ExportDir);
            System.IO.File.WriteAllBytes(ExportDir + "/preview.png", tex.EncodeToPNG());

            // 4× nearest expansion — the same uniform point upscale the GUI blit applies.
            const int s = 4;
            Color32[] src = tex.GetPixels32();
            Color32[] dst = new Color32[tex.width * s * tex.height * s];
            for (int y = 0; y < tex.height * s; y++)
            {
                int srow = (y / s) * tex.width;
                int drow = y * tex.width * s;
                for (int x = 0; x < tex.width * s; x++)
                {
                    dst[drow + x] = src[srow + x / s];
                }
            }

            Texture2D up = new Texture2D(tex.width * s, tex.height * s, TextureFormat.RGBA32, false);
            up.SetPixels32(dst);
            up.Apply(false);
            System.IO.File.WriteAllBytes(ExportDir + "/preview_4x.png", up.EncodeToPNG());
            Debug.Log(
                "[Dimensions API] Preview render exported: " + ExportDir + "/preview.png (" + rt.width + "x" + rt.height +
                ") + preview_4x.png + preview_scene.txt + preview_probe.txt");
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(up);
        }

        private static string lastCalibrationReadback;

        // The calibration strips after readback. Copied strip (raw GPU copy) at the (0,0) corner —
        // measures the READBACK path; rendered strip (known vertex colors through the real preview
        // material) at the top-right corner — measures the RENDER path. Both logged from both
        // candidate rows (top/bottom orientation differs per platform); compare each against
        // CalibrationRow: identity / sRGB-decode / sRGB-encode / anything else is read off exactly.
        private static string ReadCalibration(Texture2D tex)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            Color32[] px = tex.GetPixels32();
            for (int pass = 0; pass < 2; pass++)
            {
                int row = pass == 0 ? 0 : tex.height - 1;
                sb.Append(pass == 0 ? "copiedCalib row0:" : " | copiedCalib rowN:");
                for (int i = 0; i < CalibrationRow.Length; i++)
                {
                    Color32 c = px[row * tex.width + i];
                    sb.Append(' ').Append(c.r).Append(',').Append(c.g).Append(',').Append(c.b);
                }
            }

            sb.Append('\n');
            for (int pass = 0; pass < 2; pass++)
            {
                int row = pass == 0 ? 1 : tex.height - 2;
                sb.Append(pass == 0 ? "renderedCalib row1:" : " | renderedCalib rowN-1:");
                for (int i = 0; i < CalibrationRow.Length; i++)
                {
                    Color32 c = px[row * tex.width + (tex.width - 10 + i)];
                    sb.Append(' ').Append(c.r).Append(',').Append(c.g).Append(',').Append(c.b);
                }
            }

            return sb.ToString();
        }

        // Environment probe: records which shaders ACTUALLY resolved (the Shader.Find fallback
        // chain has been a blind spot — a session where UGC_Dummy fails silently renders with a
        // different shader), texture filter modes, color space, SRP asset and RT flags — so the
        // exported PNG is interpretable without any guessing.
        private static void WriteProbe(DimensionTilesetBlockPreview p)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("colorSpace=").Append(QualitySettings.activeColorSpace).Append('\n');
            UnityEngine.Rendering.RenderPipelineAsset srp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            sb.Append("srpAsset=").Append(srp != null ? srp.GetType().Name : "none").Append('\n');
            sb.Append("rt.sRGB=").Append(p.pixelRT != null ? p.pixelRT.sRGB.ToString() : "null").Append('\n');
            sb.Append("blockShader=").Append(p.blockMaterial != null ? p.blockMaterial.shader.name : "null").Append('\n');
            sb.Append("overlayShader=").Append(p.overlayMaterial != null ? p.overlayMaterial.shader.name : "null").Append('\n');
            sb.Append("find UGC_Dummy/Opaque=").Append(Shader.Find("UGC_Dummy/Opaque") != null).Append('\n');
            sb.Append("find SpriteObject/Simple=").Append(Shader.Find("SpriteObject/Simple") != null).Append('\n');
            sb.Append("find Sprites/Default=").Append(Shader.Find("Sprites/Default") != null).Append('\n');
            sb.Append("sheet=").Append(p.lastSheet != null ? p.lastSheet.name + " filter=" + p.lastSheet.filterMode : "null").Append('\n');
            foreach (KeyValuePair<LayerName, Texture2D> kv in p.genTextures)
            {
                sb.Append("gen ").Append(kv.Key).Append(" filter=").Append(kv.Value.filterMode)
                    .Append(" size=").Append(kv.Value.width).Append('x').Append(kv.Value.height).Append('\n');
            }

            sb.Append(lastRenderStateProbe ?? "GL.sRGBWrite: not recorded").Append('\n');
            sb.Append(lastCalibrationReadback ?? "calib: none").Append('\n');
            System.IO.File.WriteAllText(ExportDir + "/preview_probe.txt", sb.ToString());
        }

        // ---- toolbar / seed / styles ----

        private void DrawToolbar(Rect rect)
        {
            const float h = 20f;
            float pad = 6f;
            Rect ground = new Rect(rect.x + pad, rect.y + pad, 62f, h);
            Rect wall = new Rect(ground.xMax + 4f, ground.y, 50f, h);
            if (ToggleButton(ground, "Ground", palette == BlockKind.Ground))
            {
                palette = BlockKind.Ground;
            }

            if (ToggleButton(wall, "Wall", palette == BlockKind.Wall))
            {
                palette = BlockKind.Wall;
            }

            Rect reset = new Rect(rect.xMax - pad - 54f, rect.y + pad, 54f, h);
            Rect shuffle = new Rect(reset.x - 4f - 60f, reset.y, 60f, h);
            Rect edit = new Rect(shuffle.x - 4f - 46f, reset.y, 46f, h);
            if (ToggleButton(edit, "Edit", tool == Tool.Edit))
            {
                tool = tool == Tool.Edit ? Tool.View : Tool.Edit;
            }

            // Shuffle offsets the whole grid's position hash — the pattern shifts to a different coherent
            // arrangement (every tile still its intended piece), instead of rolling each block on its own.
            if (PlainButton(shuffle, "Shuffle"))
            {
                ShufflePattern();
                GUI.FocusControl(null);
            }

            if (PlainButton(reset, "⟲ Reset"))
            {
                ResetView();
                GUI.FocusControl(null);
            }
        }

        private bool ToggleButton(Rect r, string label, bool active)
        {
            Color prev = GUI.backgroundColor;
            if (active)
            {
                GUI.backgroundColor = new Color(0.90f, 0.58f, 0.26f);
            }

            bool clicked = GUI.Button(r, label, MiniButton());
            GUI.backgroundColor = prev;
            return clicked;
        }

        private bool PlainButton(Rect r, string label)
        {
            return GUI.Button(r, label, MiniButton());
        }

        private void DrawBottomText(Rect rect)
        {
            string txt = tool == Tool.Edit
                ? "left button adds · right button removes · drag to move around"
                : "click to select · drag to move around · scroll to zoom";
            GUI.Label(new Rect(rect.x, rect.yMax - 18f, rect.width, 16f), txt, BottomFaint());
        }

        private void Seed()
        {
            if (seeded)
            {
                return;
            }

            seeded = true;
            for (int x = -1; x <= 2; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    SeedCell(new Vector2Int(x, z), true, false);
                }
            }

            for (int x = -2; x <= 2; x++)
            {
                SeedCell(new Vector2Int(x, 2), true, true); // back wall (on ground)
            }

            for (int z = -1; z <= 2; z++)
            {
                SeedCell(new Vector2Int(-2, z), true, true); // left wall (on ground)
            }

            sceneDirty = true;
        }

        private void SeedCell(Vector2Int cell, bool ground, bool wall)
        {
            if (!cells.TryGetValue(cell, out Cell c))
            {
                c = new Cell();
                cells[cell] = c;
            }

            if (ground)
            {
                c.Ground = true;
            }

            if (wall)
            {
                c.Wall = true;
            }
        }

        private static void DrawBorder(Rect r)
        {
            Color c = new Color(0f, 0f, 0f, 0.4f);
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        private GUIStyle BottomFaint()
        {
            if (bottomFaint == null)
            {
                bottomFaint = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                bottomFaint.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            }

            return bottomFaint;
        }

        private GUIStyle CenteredFaint()
        {
            if (centeredFaint == null)
            {
                centeredFaint = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter };
                centeredFaint.normal.textColor = new Color(1f, 1f, 1f, 0.4f);
            }

            return centeredFaint;
        }

        private GUIStyle MiniButton()
        {
            if (miniButton == null)
            {
                miniButton = new GUIStyle(EditorStyles.miniButton) { fontSize = 11, fontStyle = FontStyle.Bold };
            }

            return miniButton;
        }
    }
}
