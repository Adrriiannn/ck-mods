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
    internal sealed partial class DimensionTilesetBlockPreview
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
