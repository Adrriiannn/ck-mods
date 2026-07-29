using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools.Generation
{
    /// <summary>
    /// Composes a block's STARTER SHEET at wizard-finish: a dirt-layout sheet carrying exactly the
    /// chosen type's base layers + ticked states (and their auto companions), copied region-by-region
    /// from the donor tileset's source sheet — nothing more, nothing less. The modder leaves the
    /// wizard with a block that already renders and plays, then repaints the sheet to make it theirs.
    ///
    /// Regions come from the captured atlas (every layer's STD/SUB/scatter coordinates), so the
    /// composed sheet is in the exact layout the extraction pipeline and the game expect. Layers with
    /// no captured coordinates (the Built types, whose art lives on other vanilla tilesets) are
    /// reported rather than silently skipped — they get starter sheets once their donor is captured.
    /// </summary>
    internal static class DimensionTilesetStarterSheet
    {
        // Auto-drawn companion passes that must ride along with a chosen layer: without their regions
        // the game would render blank fronts/shadows for it.
        private static readonly Dictionary<LayerName, LayerName[]> Companions =
            new Dictionary<LayerName, LayerName[]>
            {
                { LayerName.ground, new[] { LayerName.groundFront } },
                { LayerName.wall, new[] { LayerName.wallFront, LayerName.wallTopShadowCaster } },
                { LayerName.water, new[] { LayerName.waterFront } },
                { LayerName.ore, new[] { LayerName.oreFront } },
                { LayerName.ancientCrystal, new[] { LayerName.ancientCrystalFront } },
                { LayerName.bigRoot, new[] { LayerName.bigRootShadow, LayerName.bigRootIndirectLight } },
                { LayerName.smallGrass, new[] { LayerName.smallGrassStraws } },
                { LayerName.wallCrack, new[] { LayerName.wallCrackFront } },
                { LayerName.roofHole, new[] { LayerName.sunBeam } },
            };

        // Vanilla-completeness regions (px, image order) that no captured table references — the
        // white outline set plus a few stray sprites. Verified unreferenced by any runtime code, but
        // vanilla sheets carry them and ours should look exactly like vanilla; they also cover the
        // state-1/2 art the capture pass doesn't know yet (until capture v2 lands).
        private static readonly int[,] VanillaArtRegions =
        {
            { 0, 144, 48, 48 },   // white rounded-outline rectangle
            { 64, 144, 32, 32 },  // outlined cross
            { 0, 304, 16, 96 },   // ancient-crystal column (state variants)
            { 176, 96, 32, 32 },  // crack stroke variants
            { 256, 32, 16, 16 },
            { 128, 80, 16, 16 },
            { 112, 160, 16, 16 },
            { 80, 192, 16, 16 },
            { 304, 192, 16, 16 },
        };

        /// <summary>
        /// Composes the starter sheet for a type + chosen states. Returns false with a human-readable
        /// <paramref name="error"/> when nothing could be composed (no donor coordinates at all).
        /// <paramref name="missingLayers"/> lists included layers that had no captured coordinates.
        /// </summary>
        public static bool TryCompose(
            DimensionTilesetType type,
            IEnumerable<string> stateKeys,
            out Color32[] pixels,
            out int width,
            out int height,
            out List<LayerName> missingLayers,
            out string error)
        {
            pixels = null;
            width = 0;
            height = 0;
            missingLayers = new List<LayerName>();
            error = null;

            if (!DimensionTilesetAtlas.IsReady)
            {
                error = "Tileset atlas not captured yet — run the mod in-game once. " + DimensionTilesetAtlas.Status;
                return false;
            }

            string dirtPath = FindDirtSheetPath();
            if (string.IsNullOrEmpty(dirtPath) ||
                !DimensionTilesetPixelIo.TryLoadImageOrderFromFile(dirtPath, out Color32[] donor, out int dw, out int dh))
            {
                error = "Could not load the donor sheet (dirt_tileset.png) from the project or package cache.";
                return false;
            }

            // The full layer set to carry: the type's own layers, the ticked states' layers, and every
            // companion pass any of them needs.
            List<LayerName> included = new List<LayerName>();
            foreach (LayerName layer in type.AuthoredLayers)
            {
                AddWithCompanions(included, layer);
            }

            if (type.HasStates)
            {
                // Mirror the generator's rule exactly: every non-farm state is always included (the
                // game drives those), the farm trio only when chosen. Compose-time and bake-time must
                // agree, or a later bake reads regions the sheet never carried (the empty-GEN bug).
                HashSet<string> chosen = new HashSet<string>(stateKeys ?? new string[0]);
                foreach (DimensionTilesetState state in DimensionTilesetStateCatalog.All)
                {
                    bool isFarm = state.Key == "tilled" || state.Key == "watered" || state.Key == "flooded";
                    if (!isFarm || chosen.Contains(state.Key))
                    {
                        AddWithCompanions(included, state.Layer);
                    }
                }
            }

            // Transparent canvas in the donor's exact layout; copy each included layer's regions.
            Color32[] outPx = new Color32[dw * dh];
            List<Rect> rects = new List<Rect>();
            int copiedLayers = 0;
            foreach (LayerName layer in included)
            {
                rects.Clear();
                if (!DimensionTilesetAtlas.TryGetAllSourceRects(layer, rects))
                {
                    missingLayers.Add(layer);
                    continue;
                }

                foreach (Rect uv in rects)
                {
                    CopyRegion(donor, outPx, dw, dh, uv);
                }

                copiedLayers++;
            }

            if (copiedLayers == 0)
            {
                error = "None of this type's layers have captured donor coordinates yet — author the sheet by hand for now.";
                return false;
            }

            // Vanilla completeness: carry the sheet regions no table references, so a composed sheet
            // looks exactly like a vanilla one (and state-variant art survives until capture v2).
            for (int i = 0; i < VanillaArtRegions.GetLength(0); i++)
            {
                CopyPixelRegion(
                    donor, outPx, dw, dh,
                    VanillaArtRegions[i, 0], VanillaArtRegions[i, 1],
                    VanillaArtRegions[i, 2], VanillaArtRegions[i, 3]);
            }

            pixels = outPx;
            width = dw;
            height = dh;
            return true;
        }

        private static void AddWithCompanions(List<LayerName> included, LayerName layer)
        {
            if (!included.Contains(layer))
            {
                included.Add(layer);
            }

            if (Companions.TryGetValue(layer, out LayerName[] companions))
            {
                foreach (LayerName companion in companions)
                {
                    if (!included.Contains(companion))
                    {
                        included.Add(companion);
                    }
                }
            }
        }

        // Copies one UV region (image order, row 0 = top) from the donor into the canvas at the same
        // position. Regions may overlap between tables of one layer — same pixels either way.
        private static void CopyRegion(Color32[] src, Color32[] dst, int w, int h, Rect uv)
        {
            int x0 = Mathf.Clamp(Mathf.RoundToInt(uv.x * w), 0, w - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt((1f - (uv.y + uv.height)) * h), 0, h - 1);
            CopyPixelRegion(src, dst, w, h, x0, y0,
                Mathf.RoundToInt(uv.width * w), Mathf.RoundToInt(uv.height * h));
        }

        private static void CopyPixelRegion(Color32[] src, Color32[] dst, int w, int h, int x0, int y0, int rw, int rh)
        {
            x0 = Mathf.Clamp(x0, 0, w - 1);
            y0 = Mathf.Clamp(y0, 0, h - 1);
            rw = Mathf.Clamp(rw, 1, w - x0);
            rh = Mathf.Clamp(rh, 1, h - y0);
            for (int y = 0; y < rh; y++)
            {
                int row = (y0 + y) * w + x0;
                for (int x = 0; x < rw; x++)
                {
                    dst[row + x] = src[row + x];
                }
            }
        }

        /// <summary>Locates the vanilla dirt source sheet in the project or the package cache.</summary>
        public static string FindDirtSheetPath()
        {
            foreach (string guid in AssetDatabase.FindAssets("dirt_tileset t:Texture2D"))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath.EndsWith("dirt_tileset.png", System.StringComparison.OrdinalIgnoreCase))
                {
                    string full = Path.GetFullPath(assetPath);
                    if (File.Exists(full))
                    {
                        return full;
                    }
                }
            }

            string cache = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "PackageCache"));
            if (Directory.Exists(cache))
            {
                string[] hits = Directory.GetFiles(cache, "dirt_tileset.png", SearchOption.AllDirectories);
                if (hits.Length > 0)
                {
                    return hits[0];
                }
            }

            return null;
        }
    }
}
