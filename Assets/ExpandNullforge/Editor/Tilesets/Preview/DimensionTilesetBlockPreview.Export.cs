using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Writing the render out to disk so it can be compared with the offline one.
    /// </summary>
    internal sealed partial class DimensionTilesetBlockPreview
    {
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
    }
}
