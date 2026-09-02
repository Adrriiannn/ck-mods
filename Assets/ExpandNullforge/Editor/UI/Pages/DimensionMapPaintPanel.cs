using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The Paint tab: where a creator draws the shape of a dimension instead of accepting a
    /// rectangle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EVERYTHING BELOW THIS PANEL ALREADY WORKED. The map model, the compiler, the generation
    /// provider, the manifest field, the runtime registration and the two yield-to-painted-map
    /// guards in the other providers were all finished and correct; the only caller of
    /// <c>SetTileMap</c> was a diagnostics helper with no menu item. This panel and the one copy
    /// line in the export are the door.
    /// </para>
    /// <para>
    /// The map is written onto the LAYOUT asset, which is authoring truth. Export copies it into
    /// the generated runtime manifest so it ships in the bundle.
    /// </para>
    /// <para>
    /// You paint TILES here, not biomes. Core Keeper's biomes are rings measured from the centre
    /// of the world and cannot be given an arbitrary outline; shape belongs to the tile layer,
    /// which is what this is.
    /// </para>
    /// </remarks>
    internal sealed class DimensionMapPaintPanel
    {
        /// <summary>
        /// The largest map the snapshot format carries comfortably, per side.
        /// </summary>
        /// <remarks>
        /// A framework limit, not an engine one. The snapshot writes one JSON number per cell per
        /// layer, so the text grows with the area: 256 x 256 over a ground and a wall layer is
        /// about a quarter of a megabyte of text inside the mod's asset, and doubling the side
        /// quadruples that. Lifting it wants a run-length-encoded snapshot, which would change a
        /// format that currently works in-game.
        /// </remarks>
        internal const int MaxSide = 256;

        private readonly System.Action repaint;

        // The canvas is drawn as ONE texture, one pixel per tile, rather than a rect per cell. A
        // full 256 x 256 map over two layers is 131,072 cells, and a draw call each would repaint
        // the window at a crawl. The texture is rebuilt only when the map changes.
        private Texture2D preview;
        private bool previewStale = true;

        private DimensionTemplateAsset template;
        private DimensionLayoutTemplateAsset layout;
        private DimensionTileMapModel map;

        private int selectedPaletteIndex = -1;
        private int brushSize = 1;
        private bool showGroundLayer = true;
        private bool showWallLayer = true;
        private bool unsavedStrokes;

        private int pendingOriginX;
        private int pendingOriginY;
        private int pendingWidth = 64;
        private int pendingHeight = 64;
        private string sizeRefusal = string.Empty;

        internal DimensionMapPaintPanel(System.Action repaint)
        {
            this.repaint = repaint;
        }

        /// <summary>Builds the tab body for one layout.</summary>
        internal VisualElement Build(
            DimensionTemplateAsset dimensionTemplate,
            DimensionLayoutTemplateAsset layoutAsset)
        {
            template = dimensionTemplate;
            layout = layoutAsset;
            map = layout == null ? null : layout.PaintedTileMap;
            previewStale = true;
            if (map != null)
            {
                pendingOriginX = map.Origin.x;
                pendingOriginY = map.Origin.y;
                pendingWidth = map.Width;
                pendingHeight = map.Height;
            }

            VisualElement host = new VisualElement();
            IMGUIContainer container = new IMGUIContainer(Draw);
            container.AddToClassList("dim-canvas-island");
            host.Add(container);
            return host;
        }

        // ------------------------------------------------------------------- drawing ---

        private void Draw()
        {
            if (layout == null)
            {
                EditorGUILayout.LabelField("Open a dimension with a map to paint on it.");
                return;
            }

            Color previousBackground;
            Color previousContent;
            DimensionsApiImguiTheme.PushTint(out previousBackground, out previousContent);
            try
            {
                DrawSize();
                DrawPalette();
                DrawCanvas();
                DrawReadout();
            }
            finally
            {
                DimensionsApiImguiTheme.PopTint(previousBackground, previousContent);
            }
        }

        private void DrawSize()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("How big is this place?", EditorStyles.boldLabel);

            pendingOriginX = EditorGUILayout.IntField("Left edge", pendingOriginX);
            pendingOriginY = EditorGUILayout.IntField("Bottom edge", pendingOriginY);
            pendingWidth = EditorGUILayout.IntField("Tiles across", pendingWidth);
            pendingHeight = EditorGUILayout.IntField("Tiles down", pendingHeight);

            long cells = (long)Mathf.Max(0, pendingWidth) * Mathf.Max(0, pendingHeight);
            EditorGUILayout.LabelField(
                cells.ToString("N0") + " tiles, roughly " +
                ((cells * DimensionMapLayerRules.LayerCount * 2L) / 1024L).ToString("N0") +
                " KB of text in your mod.",
                EditorStyles.wordWrappedMiniLabel);

            if (!string.IsNullOrEmpty(sizeRefusal))
            {
                EditorGUILayout.HelpBox(sizeRefusal, MessageType.Warning);
            }

            if (GUILayout.Button(map == null ? "Start painting" : "Resize"))
            {
                ApplySize();
            }

            EditorGUILayout.EndVertical();
        }

        private void ApplySize()
        {
            string refusal;
            if (!TryPlanSize(pendingWidth, pendingHeight, out refusal))
            {
                sizeRefusal = refusal;
                return;
            }

            sizeRefusal = string.Empty;
            int2 origin = new int2(pendingOriginX, pendingOriginY);
            if (map == null)
            {
                map = new DimensionTileMapModel(origin, pendingWidth, pendingHeight);
            }
            else
            {
                // Resize, not SetBounds: everything still inside the new region survives.
                map.Resize(origin, pendingWidth, pendingHeight);
            }

            Save();
        }

        private void DrawPalette()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Blocks you can paint with", EditorStyles.boldLabel);

            if (map == null)
            {
                EditorGUILayout.LabelField(
                    "Set a size above first.",
                    EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }

            IReadOnlyList<DimensionMapBlock> palette = map.Palette;
            for (int i = 0; i < palette.Count; i++)
            {
                DimensionMapBlock block = palette[i];
                EditorGUILayout.BeginHorizontal();
                bool chosen = GUILayout.Toggle(selectedPaletteIndex == i, GUIContent.none, GUILayout.Width(18f));
                if (chosen)
                {
                    selectedPaletteIndex = i;
                }

                Rect swatch = GUILayoutUtility.GetRect(18f, 16f, GUILayout.Width(18f));
                EditorGUI.DrawRect(swatch, block.PreviewColor);
                EditorGUILayout.LabelField(block.DisplayName + "  ·  " + RoleWord(block.Role));
                EditorGUILayout.EndHorizontal();
            }

            if (palette.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "Nothing yet. Add the block this place is made of.",
                    EditorStyles.wordWrappedMiniLabel);
            }
            else
            {
                EditorGUILayout.LabelField(
                    "A block stays on this list once it is here. Clearing tiles is a right drag on " +
                    "the canvas.",
                    EditorStyles.wordWrappedMiniLabel);
            }

            if (GUILayout.Button("Add a block"))
            {
                ShowAddBlockMenu();
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// The blocks a map can be painted with: this dimension's own, and the game's own.
        /// </summary>
        /// <remarks>
        /// The role is picked FIRST because a palette entry's role cannot be changed afterwards —
        /// the entry is what every painted cell refers to. Picking it up front is honest; a
        /// dropdown on the row that silently did nothing would not be.
        /// </remarks>
        private void ShowAddBlockMenu()
        {
            GenericMenu menu = new GenericMenu();
            DimensionTileRole[] roles =
            {
                DimensionTileRole.Ground,
                DimensionTileRole.Wall,
                DimensionTileRole.Pit,
                DimensionTileRole.Liquid,
                DimensionTileRole.Ceiling,
                DimensionTileRole.Vein
            };

            DimensionTilesetAsset[] tilesets =
                template == null ? new DimensionTilesetAsset[0] : template.Tilesets;

            for (int r = 0; r < roles.Length; r++)
            {
                DimensionTileRole role = roles[r];
                string rolePath = RoleWord(role) + "/";

                bool anyOwn = false;
                for (int i = 0; i < tilesets.Length; i++)
                {
                    DimensionTilesetAsset tileset = tilesets[i];
                    if (tileset == null || !tileset.Enabled)
                    {
                        continue;
                    }

                    DimensionTilesetAsset captured = tileset;
                    DimensionTileRole capturedRole = role;
                    menu.AddItem(
                        new GUIContent(rolePath + "Your blocks/" + tileset.BlockName),
                        false,
                        () => AddPaletteEntry(
                            captured.TilesetName,
                            captured.BlockName,
                            capturedRole,
                            DimensionBlockTilesetSource.Custom,
                            0,
                            capturedRole == DimensionTileRole.Wall || capturedRole == DimensionTileRole.Vein
                                ? (Color)captured.WallMapColor
                                : (Color)captured.GroundMapColor));
                    anyOwn = true;
                }

                if (!anyOwn)
                {
                    menu.AddDisabledItem(new GUIContent(rolePath + "Your blocks/none yet"));
                }

                IReadOnlyList<DimensionVanillaTilesetEntry> vanilla = DimensionVanillaTilesetCatalog.All;
                for (int i = 0; i < vanilla.Count; i++)
                {
                    DimensionVanillaTilesetEntry entry = vanilla[i];
                    DimensionTileRole capturedRole = role;
                    int capturedIndex = entry.TilesetIndex;
                    string capturedName = entry.DisplayName;
                    menu.AddItem(
                        new GUIContent(rolePath + "The game's blocks/" + entry.DisplayName),
                        false,
                        () => AddPaletteEntry(
                            "vanilla." + capturedIndex,
                            capturedName,
                            capturedRole,
                            DimensionBlockTilesetSource.Vanilla,
                            capturedIndex,
                            RoleColor(capturedRole)));
                }
            }

            menu.ShowAsContext();
        }

        private void AddPaletteEntry(
            string tilesetKey,
            string displayName,
            DimensionTileRole role,
            DimensionBlockTilesetSource source,
            int vanillaIndex,
            Color swatch)
        {
            if (map == null)
            {
                return;
            }

            int index = map.AddBlock(new DimensionMapBlock(
                tilesetKey + "." + RoleWord(role),
                displayName + " (" + RoleWord(role) + ")",
                role,
                source,
                vanillaIndex,
                source == DimensionBlockTilesetSource.Custom ? tilesetKey : string.Empty,
                swatch));

            if (index < 0)
            {
                sizeRefusal =
                    "This map already holds 255 different blocks, which is as many as one map can " +
                    "carry.";
                return;
            }

            selectedPaletteIndex = index;
            Save();
        }

        private void DrawCanvas()
        {
            if (map == null || map.Width <= 0 || map.Height <= 0)
            {
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            brushSize = Mathf.Clamp(EditorGUILayout.IntField("Brush", brushSize), 1, 16);
            bool wasShowingGround = showGroundLayer;
            bool wasShowingWalls = showWallLayer;
            showGroundLayer = GUILayout.Toggle(showGroundLayer, "Show ground", EditorStyles.miniButton);
            showWallLayer = GUILayout.Toggle(showWallLayer, "Show walls", EditorStyles.miniButton);
            if (wasShowingGround != showGroundLayer || wasShowingWalls != showWallLayer)
            {
                previewStale = true;
            }

            EditorGUILayout.EndHorizontal();

            float aspect = map.Height / (float)map.Width;
            Rect canvas = GUILayoutUtility.GetAspectRect(1f / Mathf.Max(0.01f, aspect));
            EditorGUI.DrawRect(canvas, new Color(0.12f, 0.12f, 0.14f, 1f));

            if (Event.current.type == EventType.Repaint)
            {
                EnsurePreview();
                if (preview != null)
                {
                    GUI.DrawTexture(canvas, preview, ScaleMode.StretchToFill, true);
                }
            }

            HandleCanvasMouse(canvas);
            EditorGUILayout.LabelField(
                "Left drag paints the selected block. Right drag clears it from its own layer.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Rebuilds the one-pixel-per-tile picture of the map, if anything has changed.
        /// </summary>
        /// <remarks>
        /// The wall layer is painted over the ground layer, darkened a little, so a cell carrying
        /// both reads as a wall standing on its own ground — which is what the model stores and
        /// what the game builds. Row 0 of the texture is the map's bottom row, which is how
        /// <c>GUI.DrawTexture</c> puts it on screen and matches <see cref="ToCanvas"/>.
        /// </remarks>
        private void EnsurePreview()
        {
            if (map == null || map.Width <= 0 || map.Height <= 0)
            {
                return;
            }

            if (preview != null && (preview.width != map.Width || preview.height != map.Height))
            {
                Object.DestroyImmediate(preview);
                preview = null;
            }

            if (preview == null)
            {
                preview = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                previewStale = true;
            }

            if (!previewStale)
            {
                return;
            }

            Color32[] pixels = new Color32[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    int2 local = new int2(map.Origin.x + x, map.Origin.y + y);
                    Color colour = new Color(0f, 0f, 0f, 0f);

                    if (showGroundLayer)
                    {
                        DimensionMapBlock ground = map.GetBlock(
                            map.GetBlockIndex(local, DimensionMapLayer.Ground));
                        if (ground != null)
                        {
                            colour = ground.PreviewColor;
                            colour.a = 1f;
                        }
                    }

                    if (showWallLayer)
                    {
                        DimensionMapBlock wall = map.GetBlock(
                            map.GetBlockIndex(local, DimensionMapLayer.Wall));
                        if (wall != null)
                        {
                            Color wallColour = wall.PreviewColor;
                            colour = new Color(
                                wallColour.r * 0.75f, wallColour.g * 0.75f, wallColour.b * 0.75f, 1f);
                        }
                    }

                    pixels[x + (y * map.Width)] = colour;
                }
            }

            preview.SetPixels32(pixels);
            preview.Apply(false);
            previewStale = false;
        }

        private void HandleCanvasMouse(Rect canvas)
        {
            Event current = Event.current;
            if (current == null)
            {
                return;
            }

            // A stroke that ends outside the canvas still has to be written down, so leaving the
            // window counts as letting go.
            if ((current.type == EventType.MouseUp || current.type == EventType.MouseLeaveWindow) &&
                unsavedStrokes)
            {
                // Serializing the whole snapshot on every drag sample would re-encode the entire
                // map per mouse move; the strokes are held in the live model and written once the
                // stroke ends.
                Save();
                if (current.type == EventType.MouseUp)
                {
                    current.Use();
                }

                return;
            }

            bool stroke = current.type == EventType.MouseDown || current.type == EventType.MouseDrag;
            if (!stroke || !canvas.Contains(current.mousePosition))
            {
                return;
            }

            if (current.button != 0 && current.button != 1)
            {
                return;
            }

            DimensionMapBlock selected = map.GetBlock(selectedPaletteIndex);
            if (selected == null)
            {
                return;
            }

            int2 centre = ToLocalTile(canvas, map, current.mousePosition);
            int reach = brushSize - 1;
            for (int dy = -reach; dy <= reach; dy++)
            {
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int2 cell = new int2(centre.x + dx, centre.y + dy);
                    if (!map.InBounds(cell))
                    {
                        continue;
                    }

                    if (current.button == 0)
                    {
                        map.SetBlock(cell, selectedPaletteIndex);
                    }
                    else
                    {
                        map.ClearBlock(cell, selected.Layer);
                    }
                }
            }

            unsavedStrokes = true;
            previewStale = true;
            current.Use();
            if (repaint != null)
            {
                repaint();
            }
        }

        private void DrawReadout()
        {
            if (map == null)
            {
                return;
            }

            int painted = map.PaintedTileCount();
            EditorGUILayout.LabelField(
                painted == 0
                    ? "Nothing painted yet. While this map is empty your dimension generates the " +
                      "flat platform it always did."
                    : painted.ToString("N0") + " tiles painted. Your dimension will generate this " +
                      "shape instead of a flat platform.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(
                "You are painting tiles, not biomes. Where each biome sits is the Arrange tab.",
                EditorStyles.wordWrappedMiniLabel);
        }

        private void Save()
        {
            unsavedStrokes = false;
            previewStale = true;
            if (layout == null)
            {
                return;
            }

            layout.SetPaintedTileMap(map);
            EditorUtility.SetDirty(layout);
        }

        // ------------------------------------------------------------------- pure helpers ---

        /// <summary>
        /// Whether a requested map size is one the snapshot can carry, and why not when it is not.
        /// </summary>
        internal static bool TryPlanSize(int width, int height, out string refusal)
        {
            if (width < 1 || height < 1)
            {
                refusal = "A map needs at least one tile across and one tile down.";
                return false;
            }

            if (width > MaxSide || height > MaxSide)
            {
                refusal =
                    "A painted map goes up to " + MaxSide + " tiles across and " + MaxSide +
                    " down. Past that the map is stored as more text than is sensible to ship " +
                    "inside a mod. Paint the shape that matters and let the rest generate.";
                return false;
            }

            refusal = string.Empty;
            return true;
        }

        /// <summary>How many screen pixels one tile takes, so the whole map fits the canvas.</summary>
        internal static float CellSize(Rect canvas, DimensionTileMapModel model)
        {
            if (model == null || model.Width <= 0 || model.Height <= 0)
            {
                return 0f;
            }

            return Mathf.Min(canvas.width / model.Width, canvas.height / model.Height);
        }

        /// <summary>
        /// The canvas point at the CENTRE of a local tile.
        /// </summary>
        /// <remarks>
        /// The centre rather than a corner, so <see cref="ToLocalTile"/> is its exact inverse for
        /// every tile: a corner sits on the boundary between two cells and rounds either way.
        /// Y is flipped because canvas space grows downward and tile space grows upward.
        /// </remarks>
        internal static Vector2 ToCanvas(Rect canvas, DimensionTileMapModel model, int2 localTile)
        {
            float cell = CellSize(canvas, model);
            int column = localTile.x - model.Origin.x;
            int row = localTile.y - model.Origin.y;
            return new Vector2(
                canvas.xMin + ((column + 0.5f) * cell),
                canvas.yMin + ((model.Height - 1 - row + 0.5f) * cell));
        }

        /// <summary>The local tile under a canvas point, clamped to the map.</summary>
        internal static int2 ToLocalTile(Rect canvas, DimensionTileMapModel model, Vector2 point)
        {
            float cell = CellSize(canvas, model);
            if (cell <= 0f)
            {
                return model == null ? int2.zero : model.Origin;
            }

            int column = Mathf.Clamp(
                Mathf.FloorToInt((point.x - canvas.xMin) / cell), 0, model.Width - 1);
            int flippedRow = Mathf.Clamp(
                Mathf.FloorToInt((point.y - canvas.yMin) / cell), 0, model.Height - 1);
            int row = model.Height - 1 - flippedRow;
            return new int2(model.Origin.x + column, model.Origin.y + row);
        }

        /// <summary>The word a creator reads for a tile role.</summary>
        internal static string RoleWord(DimensionTileRole role)
        {
            switch (role)
            {
                case DimensionTileRole.Wall:
                    return "Wall";
                case DimensionTileRole.Pit:
                    return "Pit";
                case DimensionTileRole.Liquid:
                    return "Water";
                case DimensionTileRole.Ceiling:
                    return "Skylight";
                case DimensionTileRole.Vein:
                    return "Ore vein";
                default:
                    return "Ground";
            }
        }

        private static Color RoleColor(DimensionTileRole role)
        {
            switch (role)
            {
                case DimensionTileRole.Wall:
                    return new Color(0.40f, 0.33f, 0.26f, 1f);
                case DimensionTileRole.Pit:
                    return new Color(0.08f, 0.08f, 0.10f, 1f);
                case DimensionTileRole.Liquid:
                    return new Color(0.20f, 0.45f, 0.70f, 1f);
                case DimensionTileRole.Ceiling:
                    return new Color(0.85f, 0.82f, 0.60f, 1f);
                case DimensionTileRole.Vein:
                    return new Color(0.55f, 0.45f, 0.20f, 1f);
                default:
                    return new Color(0.48f, 0.36f, 0.24f, 1f);
            }
        }
    }
}
