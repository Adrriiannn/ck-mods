using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools.Generation
{
    /// <summary>
    /// Bakes a custom tileset's own full-adaptive ("GEN") sheets from the modder's source sheet and
    /// stores them on the <see cref="DimensionTilesetAsset"/> so the "mint" files ship in the bundle.
    ///
    /// For each base layer it composites all 256 neighbour masks (Core Keeper's own hybrid bake:
    /// authored mask → one full tile, else four 8×8 sub-corner quarters) and packs them into Core
    /// Keeper's exact canonical GEN layout — so the game's own lookup table samples the result natively
    /// and the tileset is a genuine, portable Core Keeper tileset rather than one locked to this
    /// framework. <see cref="DimensionTilesetAssetRuntime"/> then feeds each baked sheet into the live
    /// tileset's <c>AdaptiveTextures[layer]</c>, which is the whole native-rendering allocation.
    /// </summary>
    public static class DimensionTilesetGenerator
    {
        /// <summary>
        /// Bake and store the GEN sheets for one tileset asset. Returns false with a human-readable
        /// <paramref name="error"/> on any failure (no partial state is committed). Editor-only.
        /// </summary>
        public static bool Generate(DimensionTilesetAsset asset, out string error)
        {
            error = null;

            if (asset == null)
            {
                error = "No tileset selected.";
                return false;
            }

            if (asset.TilesetTexture == null)
            {
                error = "Assign a tileset texture before generating.";
                return false;
            }

            if (!DimensionTilesetAtlas.IsReady)
            {
                error = "Tileset atlas not captured yet — run the mod in-game once so the vanilla layout is baked. " +
                        DimensionTilesetAtlas.Status;
                return false;
            }

            if (!DimensionTilesetPixelIo.TryLoadImageOrder(asset.TilesetTexture, out Color32[] src, out int sw, out int sh))
            {
                error = "Could not read '" + asset.TilesetTexture.name + "' as a PNG. Custom tileset sheets must be PNG files.";
                return false;
            }

            // Captured coordinates are fractions of the donor layout, so a differently sized sheet
            // reads the wrong pixels at every tile and bakes plausible-looking garbage. Refuse instead.
            if (DimensionTilesetAtlas.TryGetSourceSheetSize(out int expectW, out int expectH) &&
                (sw != expectW || sh != expectH))
            {
                error = "'" + asset.TilesetTexture.name + "' is " + sw + "x" + sh + ", but the vanilla layout is " +
                        expectW + "x" + expectH + ". Every tile is read at a fixed spot on that layout — resize the " +
                        "canvas (don't scale the art) so the sheet matches, then generate again.";
                return false;
            }

            // An emissive tileset bakes a SECOND GEN per layer from the emissive sheet (same layout,
            // pixels = emitted light) — the game samples full-adaptive layers' glow through the same
            // packed-GEN path as their color, so without this bake they would never glow.
            Color32[] emissiveSrc = null;
            int esW = 0, esH = 0;
            if (asset.IsEmissive && asset.EmissiveTexture != null &&
                !DimensionTilesetPixelIo.TryLoadImageOrder(asset.EmissiveTexture, out emissiveSrc, out esW, out esH))
            {
                error = "Could not read the emissive sheet '" + asset.EmissiveTexture.name + "' as a PNG.";
                return false;
            }

            if (emissiveSrc != null && (esW != sw || esH != sh))
            {
                error = "The emissive sheet must match the tileset sheet's size (" + sw + "x" + sh +
                        "), but is " + esW + "x" + esH + ".";
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                error = "The tileset asset must be saved to the project before generating.";
                return false;
            }

            string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string folderName = asset.name + "_Generated";
            string genDir = dir + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(genDir))
            {
                AssetDatabase.CreateFolder(dir, folderName);
            }

            List<DimensionGeneratedGenLayer> baked = new List<DimensionGeneratedGenLayer>();
            foreach (LayerName layer in ResolveLayersToBake(asset))
            {
                int connect = DimensionTilesetAtlas.TryGetLayer(layer, out DimensionTilesetAtlas.LayerInfo info)
                    ? info.ConnectBits
                    : 255;

                DimensionAtlasGenLayout layout = DimensionAtlasGenLayout.TryCreate(layer);
                if (layout == null)
                {
                    // Only ground/wall are force-added without a pre-check; an optional layer only
                    // reaches this loop when its layout already resolved, so this is a real capture gap.
                    error = "No captured GEN layout for the '" + layer + "' layer — re-capture the atlas in-game.";
                    return false;
                }

                DimensionDirtTileSource source = new DimensionDirtTileSource(layer, src, sw, sh);
                DimensionTilesetCompositor.GenLayer gen = DimensionTilesetCompositor.Composite(source, connect, layout);

                string pngPath = genDir + "/" + layer + "_gen.png";
                Texture2D tex = DimensionTilesetPixelIo.SaveImageOrderPng(gen.Pixels, gen.Width, gen.Height, pngPath);
                if (tex == null)
                {
                    error = "Failed to write the baked '" + layer + "' GEN sheet.";
                    return false;
                }

                DimensionGeneratedGenLayer entry = new DimensionGeneratedGenLayer { layer = layer, texture = tex };

                if (emissiveSrc != null)
                {
                    DimensionDirtTileSource emissiveSource = new DimensionDirtTileSource(layer, emissiveSrc, sw, sh);
                    DimensionTilesetCompositor.GenLayer emissiveGen =
                        DimensionTilesetCompositor.Composite(emissiveSource, connect, layout);
                    entry.emissiveTexture = DimensionTilesetPixelIo.SaveImageOrderPng(
                        emissiveGen.Pixels, emissiveGen.Width, emissiveGen.Height,
                        genDir + "/" + layer + "_gen_emissive.png");
                    if (entry.emissiveTexture == null)
                    {
                        error = "Failed to write the baked '" + layer + "' emissive GEN sheet.";
                        return false;
                    }
                }

                baked.Add(entry);
            }

            // Prune any stale baked sheets from a previous, wider generation (e.g. a state the modder
            // has since switched off) so the folder and the asset stay in lockstep with what's enabled.
            PruneStaleGeneratedSheets(genDir, baked);

            asset.EditorSetGeneratedGen(baked);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        /// <summary>
        /// The layers to bake for this tileset, driven by its TYPE: the type's own authored layers,
        /// plus (terrain only) every full-adaptive layer whose state the modder switched on.
        /// Bake membership is gated by <see cref="DimensionAtlasGenLayout.TryCreate"/> being non-null —
        /// that is exactly the set of layers the engine samples through <c>GetAdaptiveTexture</c> (the
        /// packed-GEN path). RandomFill scatter states (pebbles, debris, cracks, ore, vines) and
        /// STD-only faces (fronts, roof/sun light) are intentionally excluded: they render straight
        /// from the main sheet at source-layout coords via <c>GetTexture</c>, so a packed GEN would be
        /// wrong for them, not merely unnecessary. An enabled "roots" state also drags in big-root's
        /// shadow + indirect-light companion passes, which the engine renders together with it.
        /// </summary>
        private static List<LayerName> ResolveLayersToBake(DimensionTilesetAsset asset)
        {
            List<LayerName> set = new List<LayerName>();
            DimensionTilesetType type = asset.BlockType;

            // The type's own layers first (terrain: ground+wall; water: water; floor: floor; …),
            // keeping only the full-adaptive ones — the rest render from the main sheet unbaked.
            foreach (LayerName layer in type.AuthoredLayers)
            {
                if (DimensionAtlasGenLayout.TryCreate(layer) != null)
                {
                    AddUnique(set, layer);
                }
            }

            if (type.HasStates)
            {
                // Every full-adaptive state is baked, switched on or not. A state toggle governs
                // whether the game can PRODUCE that tile, never whether its art exists — and a
                // full-adaptive layer with no baked sheet is actively harmful: GetAdaptiveTexture
                // returns null and the renderer then falls back to the shared layer template's own
                // texture, painting the tile with fragments of an unrelated vanilla tileset.
                foreach (DimensionTilesetState state in DimensionTilesetStateCatalog.All)
                {
                    if (DimensionAtlasGenLayout.TryCreate(state.Layer) == null)
                    {
                        continue; // not a full-adaptive GEN layer → renders from the main sheet, no bake
                    }

                    AddUnique(set, state.Layer);
                }
            }

            if (set.Contains(LayerName.bigRoot))
            {
                AddUnique(set, LayerName.bigRootShadow);
                AddUnique(set, LayerName.bigRootIndirectLight);
            }

            return set;
        }

        private static void AddUnique(List<LayerName> set, LayerName layer)
        {
            if (!set.Contains(layer))
            {
                set.Add(layer);
            }
        }

        // Delete baked "*_gen.png" sheets in the generated folder that the current bake no longer
        // produced (a state was switched off since last time), keeping the folder mint.
        private static void PruneStaleGeneratedSheets(string genDir, List<DimensionGeneratedGenLayer> baked)
        {
            HashSet<string> keep = new HashSet<string>();
            foreach (DimensionGeneratedGenLayer b in baked)
            {
                keep.Add(b.layer + "_gen.png");
                if (b.emissiveTexture != null)
                {
                    keep.Add(b.layer + "_gen_emissive.png");
                }
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { genDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileName(path);
                bool isBakedSheet =
                    name.EndsWith("_gen.png", System.StringComparison.Ordinal) ||
                    name.EndsWith("_gen_emissive.png", System.StringComparison.Ordinal);
                if (isBakedSheet && !keep.Contains(name))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }
    }
}
