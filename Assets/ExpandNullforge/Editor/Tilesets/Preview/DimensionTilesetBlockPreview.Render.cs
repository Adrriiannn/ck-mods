using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Rendering the block into its own low resolution target, and the materials it needs.
    /// </summary>
    internal sealed partial class DimensionTilesetBlockPreview
    {
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
    }
}
