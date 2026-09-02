using System.IO;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools.Generation
{
    /// <summary>
    /// Regenerates Dirt's ground and wall GEN sheets and writes them to disk to look at.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The pass/fail question — does our compositor reproduce Core Keeper's own bake — is answered by
    /// <c>DimensionTilesetGenerationTests</c>, which runs on every test pass and asserts every generated
    /// tile is byte-identical to a real vanilla one. This menu item exists for the other half: when a
    /// change does break parity, a number in a test log will not tell you which corner went wrong. The
    /// regenerated atlas dropped under <c>Library/ExpandNullforge/GoldenTest/</c> will, next to the
    /// shipped sheet.
    /// </para>
    /// <para>
    /// Both paths measure through <see cref="DimensionTilesetGoldenAssets"/>, so the picture written here
    /// and the assertion made there are about the same pixels.
    /// </para>
    /// </remarks>
    internal static class DimensionTilesetGoldenRegenMenu
    {
        [MenuItem("Dimensions API/Debug/Golden Test — Regenerate Dirt GEN")]
        private static void Run()
        {
            if (!DimensionTilesetAtlas.IsReady)
            {
                Debug.LogError(
                    "[NF Golden] Atlas not captured — run the mod in-game once to bake DimensionTilesetAtlasData. " +
                    DimensionTilesetAtlas.Status);
                return;
            }

            Color32[] dirt;
            int dw, dh;
            if (!DimensionTilesetGoldenAssets.TryLoadPixels("dirt_tileset", out dirt, out dw, out dh))
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

        private static void RunLayer(
            LayerName layer,
            string shippedName,
            Color32[] dirt,
            int dw,
            int dh,
            string outDir)
        {
            DimensionTilesetCompositor.GenLayer gen =
                DimensionTilesetGoldenAssets.RegenerateVanillaLayer(layer, dirt, dw, dh);
            DimensionAtlasGenLayout layout = DimensionAtlasGenLayout.TryCreate(layer);
            Debug.Log("[NF Golden] " + layer + ": packing = " +
                      (layout != null ? "VANILLA layout " + layout.Width + "x" + layout.Height : "own grid"));

            // Flip image-order → texture y-up so the written PNG is upright when opened.
            Color32[] savePixels = (Color32[])gen.Pixels.Clone();
            DimensionTilesetGoldenAssets.FlipVertical(savePixels, gen.Width, gen.Height);
            Texture2D tex = new Texture2D(gen.Width, gen.Height, TextureFormat.RGBA32, false);
            tex.SetPixels32(savePixels);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(outDir, layer + "_regen.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            Color32[] shipped;
            int sw, sh;
            if (!DimensionTilesetGoldenAssets.TryLoadPixels(shippedName, out shipped, out sw, out sh))
            {
                Debug.LogWarning("[NF Golden] " + layer + ": regenerated (" + gen.Width + "x" + gen.Height +
                                 ") but shipped " + shippedName + ".png not found for comparison.");
                return;
            }

            int identical = 0;
            int total = 0;
            float worst = 0f;
            int worstMask = -1;
            for (int mask = 0; mask < 256; mask++)
            {
                for (int variant = 0; variant < gen.Uvs[mask].Count; variant++)
                {
                    total++;
                    Color32[] tile = DimensionTilesetGoldenAssets.CellFromUv(
                        gen.Pixels, gen.Width, gen.Height, gen.Uvs[mask][variant]);
                    float best = DimensionTilesetGoldenAssets.BestRmseAnywhere(tile, shipped, sw, sh);
                    if (best <= 0f)
                    {
                        identical++;
                    }
                    else if (best > worst)
                    {
                        worst = best;
                        worstMask = mask;
                    }
                }
            }

            Debug.Log("[NF Golden] " + layer + ": " + identical + "/" + total +
                      " generated tiles are byte-identical to a shipped vanilla tile" +
                      (worstMask >= 0
                          ? ". Worst mismatch is mask " + worstMask + " at RMSE " + worst.ToString("F2") +
                            " — compare " + layer + "_regen.png against " + shippedName + ".png."
                          : ".") +
                      " (atlas " + gen.Width + "x" + gen.Height + ", shipped " + sw + "x" + sh + ")");
        }
    }
}
