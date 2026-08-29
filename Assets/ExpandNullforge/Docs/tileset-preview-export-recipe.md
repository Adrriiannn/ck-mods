# Diffing Unity's real render against the offline simulator

The tileset block preview is drawn twice in this project: once by Unity, through the project's real
render pipeline, and once by an offline reproduction used to reason about what a sheet will look
like without opening the editor. When the two disagree, the question is always *which link in the
chain moved the pixels* — the shader, the sRGB write, the tonemap, or the readback.

`DimensionTilesetBlockPreview.ExportPreviewRender` and its four helpers are the written-down answer
to that question. This page is the recipe; the code is relocated to
`Editor/DimensionTilesetPreviewExport.cs` with a menu item under
**Dimensions API ▸ Developer ▸ Export Preview Render** so it can actually be run.

## What it writes

Everything lands in `Library/ExpandNullforge/PreviewExport/`:

| File | What it is |
|---|---|
| `preview.png` | The RT's raw bytes, at render resolution. |
| `preview_4x.png` | The same, 4× nearest-neighbour — the identical uniform point upscale the GUI blit applies. |
| `preview_scene.txt` | The scene and camera state that produced it. |
| `preview_probe.txt` | What the environment actually resolved to, plus the calibration readback. |

It uses the last-drawn preview instance — the Tileset Studio's, with its live edit state — so what
you export is what you were looking at. With no Studio open it builds a default seeded scene from
the selected `DimensionTilesetAsset` and cleans it up afterwards.

## The four things that make it a recipe rather than a screenshot

### 1. The scene dump removes the reverse-engineering step

`preview_scene.txt` records `orthoSize`, `pivot` (all three components, round-trip `"R"` format),
`patternShift`, whether jitter is on, the draw rect, and then one line per occupied cell with its
ground/wall flags and its state lists.

That is everything an offline reproduction needs to build the same scene. Without it, matching a PNG
means guessing the pan and zoom from the image, which is how a mismatch gets misattributed to the
shader.

### 2. Two calibration strips, measuring two different halves of the chain

A single known-value strip only tells you what the *readback* did to it. This writes two:

- **The copied strip.** Eight known `Color32` values — black, 32, 64, 128, 192, white, red, blue —
  pushed into the RT's `(0,0)` corner with `Graphics.CopyTexture`, a raw GPU byte copy with no
  colorspace math. Whatever the readback path does shows up on these exactly.
- **The rendered strip.** The same known values driven through the real preview material as vertex
  colours into the RT's top-right corner, during the export render only (`exportProbeActive`).

`ReadCalibration` reads both from both candidate rows — row 0 and row N, row 1 and row N−1, because
readback orientation differs per platform — and writes all four rows into `preview_probe.txt`.

**Comparing the two against the known input separates the sample/write transform from the readback
transform.** Identity, sRGB-decode, sRGB-encode or something else is read off the numbers instead of
inferred. That is the whole trick, and it is why this is worth keeping.

### 3. The readback texture's colorspace flag matches the RT's

```csharp
Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, !rt.sRGB);
```

A mismatch here makes Unity convert during `ReadPixels`, which was corrupting export brightness. The
PNG is the RT's raw bytes and needs no interpretation; the probe file states what transform, if any,
the readback applied.

### 4. The environment probe records what actually resolved

`Shader.Find` falling back silently has been a blind spot — a session where `UGC_Dummy` fails to
resolve renders through a different shader and looks like a data problem. `preview_probe.txt`
records:

```
colorSpace          QualitySettings.activeColorSpace
srpAsset            the active render pipeline asset's type name, or "none"
rt.sRGB             the RT's sRGB flag
blockShader         the shader the block material actually has
overlayShader       the shader the overlay material actually has
find UGC_Dummy/Opaque, find SpriteObject/Simple, find Sprites/Default
sheet               name and filter mode
gen <layer>         filter mode and size, per generated layer texture
GL.sRGBWrite        the incoming state, recorded before the render forced it
the calibration readback
```

## The two render-state facts this pipeline exists to have found

Both are recorded in the render path itself and both are the sort of thing only a calibrated export
finds.

**The SRP must be bypassed.** Calling `cam.Render()` directly sends the preview camera through the
project's active SRP — this SDK ships PugRP — whose tonemap and resolve chain both shifts every
colour and blends art-pixel edges. Measured: flat art regions came back remapped, background lifted
by +68, and a pixel-exact offline reproduction of one exported scene matched everywhere except
tile-boundary pixels, where the export held 76 blend colours a 14-colour palette cannot produce.
Those were the long-standing "brown marks at cap junctions". `PreviewRenderUtility.Render()` flips
this switch itself; calling `cam.Render()` means flipping it yourself.

**`GL.sRGBWrite` must be forced on for the render.** In a Linear project an sRGB-flagged RT only
gamma-encodes shader output while that state is on. Editor GUI code commonly leaves it off, which
writes linear values raw into the sRGB RT — geometry comes out darkened downstream while raw copies
stay byte-perfect, which is exactly the signature that makes the two calibration strips readable.
Record the incoming state for the probe, force it on, restore after.

## How to use it

1. Open the Tileset Studio and get the preview into the state you care about.
2. Run **Dimensions API ▸ Developer ▸ Export Preview Render**.
3. Read `preview_probe.txt` first. If the copied strip is not identity, the readback is transforming
   and the PNG is not raw. If the copied strip is identity and the rendered strip is not, the
   transform is in the render — shader or sRGB write.
4. Rebuild the scene offline from `preview_scene.txt` and diff against `preview.png`. Anything left
   is a genuine disagreement between the simulator and the engine.
