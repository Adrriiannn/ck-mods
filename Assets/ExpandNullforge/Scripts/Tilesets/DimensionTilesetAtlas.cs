using System;
using System.Collections.Generic;
using System.Globalization;
using PugTilemap;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// Edit-time reader for Core Keeper's real dirt tileset, baked into <see cref="DimensionTilesetAtlasData"/>
    /// from an in-game capture (<see cref="DimensionTilesetAtlasCapture"/>). Exposes, per layer: its config
    /// (fill type, the face it draws on, vertical offset/stretch, what tile it targets), its 9-way adaptive
    /// lookup (start/length sub-lists into a flat sprite-coord array, masked by the layer's available
    /// directions and varied by a position random — as <c>QuadGeneratorExtensions.ResolveQuad</c> does),
    /// and its RandomFill sprite list. All coords are 0-1 UVs in dirt_tileset.png. No file I/O, because the bake is
    /// C# literals rather than data. The sandbox bans System.IO but not writing: the game's own
    /// <c>API.ConfigFilesystem</c> is the door, and this framework writes its save files through it.
    /// An empty bake ⇒ callers fall back.
    /// </summary>
    public static class DimensionTilesetAtlas
    {
        public enum FaceKind
        {
            Top,
            Bottom,
            Side,
        }

        internal sealed class Adaptive
        {
            public int DirBits;
            public Rect[] Coords;
            public int[] Start;   // 256
            public int[] Length;  // 256

            /// <summary>Whether this table actually authored a sprite for direction-mask <paramref name="m"/>.</summary>
            public bool Has(int m)
            {
                return m >= 0 && m < 256 && Length[m] > 0;
            }
        }

        public sealed class LayerInfo
        {
            public LayerName Layer;
            public int Fill;          // 0 NoFill,1 RandomFill,2 AdaptativeFill,3 AdaptativeExtrude,4 CustomFill,5 RandomFillEditorOnly
            public FaceKind Face;
            public float OffY;
            public float HStretch;
            public LayerName Target;
            public bool HasTarget;
            public LayerName Data;
            public bool IsDataLayer;
            public bool Emissive;
            internal Adaptive Std;
            internal Adaptive Sub;
            internal Rect[] Random;
            internal Adaptive Gen;   // vanilla's baked full-adaptive lookup (canonical packing)
            internal int GenW;       // GEN texture width  (always 256)
            internal int GenH;       // GEN texture height (per layer, 16px rows)
            internal Dictionary<int, Rect[]> Learned; // (mask<<8)|variant → the 4 quarter UVs vanilla's bake picked (TL,TR,BL,BR)

            /// <summary>Whether this layer resolves adaptively (has a 9-way or sub-tile lookup).</summary>
            public bool Adaptive
            {
                get { return Std != null || Sub != null; }
            }

            /// <summary>Whether this layer has a sub-tile (quarter) lookup — composite it four quarters at a time.</summary>
            public bool HasSubtile
            {
                get { return Sub != null; }
            }

            /// <summary>
            /// The directions this layer actually connects along (its 9-way dirBits), e.g. 85 for an
            /// orthogonal-only trail like slime. Mask a raw 8-way neighbour bitmask by this before
            /// deriving sub-corner masks, or diagonal-only adjacency invents combinations the sub-tile
            /// table never baked (leaving holes).
            /// </summary>
            public int ConnectBits
            {
                get { return Std != null ? Std.DirBits : Sub != null ? Sub.DirBits : 255; }
            }

            /// <summary>Where a horizontal (Top/Bottom) layer's quad sits; for a Side layer this is the top of the face.</summary>
            public float TopY
            {
                get { return Face == FaceKind.Bottom ? OffY : OffY + 1f + HStretch; }
            }

            /// <summary>The bottom of a Side layer's face.</summary>
            public float BottomY
            {
                get { return OffY; }
            }
        }

        private static readonly Dictionary<LayerName, LayerInfo> layers = new Dictionary<LayerName, LayerInfo>();
        private static bool built;
        private static int sourceW;
        private static int sourceH;

        public static bool IsReady
        {
            get { Build(); return layers.Count > 0; }
        }

        /// <summary>
        /// The size of the source sheet the captured coordinates address — the vanilla donor layout
        /// every authored tileset sheet must match. All UVs are fractions of this, so a sheet of any
        /// other size samples the wrong pixels rather than failing loudly.
        /// </summary>
        public static bool TryGetSourceSheetSize(out int width, out int height)
        {
            Build();
            width = sourceW;
            height = sourceH;
            return sourceW > 0 && sourceH > 0;
        }

        public static string Status
        {
            get { Build(); return layers.Count > 0 ? "ready (" + layers.Count + " layers)" : "not captured — run the mod in-game once"; }
        }

        /// <summary>All layers present in the bake, for building the state catalog.</summary>
        public static IEnumerable<LayerInfo> Layers
        {
            get { Build(); return layers.Values; }
        }

        public static bool TryGetLayer(LayerName layer, out LayerInfo info)
        {
            Build();
            return layers.TryGetValue(layer, out info);
        }

        /// <summary>The fully-surrounded (interior fill) sprite of a layer.</summary>
        public static bool TryGetLayerUV(LayerName layer, out Rect uv)
        {
            return TryGetAdaptiveUV(layer, 0xFF, 0, out uv);
        }

        /// <summary>
        /// Every captured source-sheet UV rect this layer draws from — its 9-way (STD) sprites,
        /// sub-tile (SUB) quarters and random/scatter sprites. This is the layer's complete footprint
        /// on the source sheet, used to compose starter sheets that carry exactly the chosen layers.
        /// Returns false when the layer has no captured coordinates at all.
        /// </summary>
        public static bool TryGetAllSourceRects(LayerName layer, List<Rect> results)
        {
            Build();
            if (results == null || !layers.TryGetValue(layer, out LayerInfo info))
            {
                return false;
            }

            int before = results.Count;
            if (info.Std != null && info.Std.Coords != null)
            {
                results.AddRange(info.Std.Coords);
            }

            if (info.Sub != null && info.Sub.Coords != null)
            {
                results.AddRange(info.Sub.Coords);
            }

            if (info.Random != null)
            {
                results.AddRange(info.Random);
            }

            return results.Count > before;
        }

        /// <summary>
        /// The sprite for a tile of <paramref name="layer"/> whose same-layer neighbours are
        /// <paramref name="dirFlags"/> (raw 8-way), varied by <paramref name="random"/> — 1:1 with the game.
        /// </summary>
        public static bool TryGetAdaptiveUV(LayerName layer, int dirFlags, int random, out Rect uv)
        {
            uv = default;
            Build();
            return layers.TryGetValue(layer, out LayerInfo info) && Lookup(info.Std, dirFlags, random, out uv);
        }

        /// <summary>
        /// True if the 9-way (STD) table authored a sprite for this exact neighbour mask (after restricting to
        /// the layer's connect directions). Core Keeper draws ONE full sprite per tile
        /// (<c>QuadGeneratorExtensions.ResolveQuad</c>); the authored masks — interiors, straight edges, convex
        /// corners — are those full tiles. Masks it did NOT author (concave inner corners, thin-wall segments,
        /// end-caps) are the ones the bake fills by compositing sub-tile quarters, so the caller should composite
        /// those from <see cref="TryGetSubtileUV"/> instead of forcing them onto a wrong full sprite.
        /// </summary>
        public static bool IsAuthoredAdaptive(LayerName layer, int rawFlags)
        {
            Build();
            if (!layers.TryGetValue(layer, out LayerInfo info) || info.Std == null)
            {
                return false;
            }

            return info.Std.Has(rawFlags & info.Std.DirBits);
        }

        /// <summary>
        /// How many distinct variants the 9-way (STD) table authored for this mask (0 if unauthored). Core
        /// Keeper's position hash picks among these per tile, so a full sprite is not repeated across a field —
        /// the generator must emit every one, or the tileset looks obviously tiled.
        /// </summary>
        public static int AdaptiveVariantCount(LayerName layer, int rawFlags)
        {
            Build();
            if (layers.TryGetValue(layer, out LayerInfo info) && info.Std != null)
            {
                return info.Std.Length[rawFlags & info.Std.DirBits & 0xFF];
            }

            return 0;
        }

        /// <summary>How many variants the sub-tile table authored for a sub-corner mask (0 if none).</summary>
        public static int SubtileVariantCount(LayerName layer, int subMask)
        {
            Build();
            if (layers.TryGetValue(layer, out LayerInfo info) && info.Sub != null)
            {
                return info.Sub.Length[subMask & info.Sub.DirBits & 0xFF];
            }

            return 0;
        }

        /// <summary>Whether vanilla's canonical GEN packing was captured for this layer.</summary>
        public static bool HasGenLayout(LayerName layer)
        {
            Build();
            return layers.TryGetValue(layer, out LayerInfo info) && info.Gen != null && info.GenW > 0 && info.GenH > 0;
        }

        /// <summary>Vanilla's baked variant count for this mask in the canonical GEN layout (0 if no GEN table).</summary>
        public static int GenVariantCount(LayerName layer, int rawFlags)
        {
            Build();
            if (layers.TryGetValue(layer, out LayerInfo info) && info.Gen != null)
            {
                return info.Gen.Length[rawFlags & info.Gen.DirBits & 0xFF];
            }

            return 0;
        }

        /// <summary>The canonical GEN texture size for this layer (256 × per-layer height).</summary>
        public static bool TryGetGenSize(LayerName layer, out int width, out int height)
        {
            Build();
            if (layers.TryGetValue(layer, out LayerInfo info) && info.Gen != null && info.GenW > 0 && info.GenH > 0)
            {
                width = info.GenW;
                height = info.GenH;
                return true;
            }

            width = 0;
            height = 0;
            return false;
        }

        /// <summary>
        /// Image-order (row 0 = top) top-left pixel of the 16px cell vanilla packs (layer, mask, variant) into.
        /// Placing our composited tile here reproduces Core Keeper's exact sheet layout, so the output is a real
        /// Core Keeper tileset (portable), not a framework-only one.
        /// </summary>
        public static bool TryGetGenCell(LayerName layer, int rawFlags, int variant, out int leftPx, out int topPx)
        {
            leftPx = 0;
            topPx = 0;
            Build();
            if (!layers.TryGetValue(layer, out LayerInfo info) || info.Gen == null || info.GenW <= 0 || info.GenH <= 0)
            {
                return false;
            }

            if (!Lookup(info.Gen, rawFlags, variant, out Rect uv))
            {
                return false;
            }

            leftPx = Mathf.RoundToInt(uv.x * info.GenW / 16f) * 16;
            topPx = Mathf.RoundToInt((1f - (uv.y + uv.height)) * info.GenH / 16f) * 16;
            return true;
        }

        /// <summary>
        /// The RAW captured GEN uv rect for (layer, mask, variant) — vanilla's own lookup entry, y-up
        /// UVs into the canonical 256×GenH sheet. Unlike <see cref="TryGetGenCell"/> (which derives the
        /// cell's full 16px pixel bounds for PLACING baked pixels), this returns the rect vanilla
        /// SAMPLES with: ~15.75px wide with an ~0.125px anti-bleed inset per side (captured
        /// uv.w = 0.0615234·256 = 15.75), so edge samples never interpolate into the neighbouring
        /// sheet cell. Baked GEN PNGs are written in exactly this layout and size, so the captured
        /// rect addresses them as-is. Same DirBits masking + variant-modulo as every other lookup.
        /// </summary>
        public static bool TryGetGenUVRect(LayerName layer, int rawFlags, int variant, out Rect uv)
        {
            uv = default;
            Build();
            return layers.TryGetValue(layer, out LayerInfo info) &&
                   info.Gen != null && info.GenW > 0 && info.GenH > 0 &&
                   Lookup(info.Gen, rawFlags, variant, out uv);
        }

        /// <summary>
        /// The LEARNED source-sheet quarter for one corner of a composited (unauthored) mask — the exact
        /// 8x8 rect vanilla's own bake picked for (mask, variant), mined offline from the shipped GEN
        /// sheets (byte-identical matches). <paramref name="corner"/>: 0 TL, 1 TR, 2 BL, 3 BR (image
        /// order). Returns false when no recipe was learned for this (layer, mask, variant) — the caller
        /// falls back to the derived sub-corner-mask rule. The UV is template-relative, so sampling it
        /// from a modder's sheet reproduces vanilla's quarter picks with the modder's art.
        /// </summary>
        public static bool TryGetLearnedQuarterUV(LayerName layer, int mask, int variant, int corner, out Rect uv)
        {
            uv = default;
            Build();
            if (corner < 0 || corner > 3 || mask < 0 || mask > 255 || variant < 0 || variant > 255)
            {
                return false;
            }

            if (!layers.TryGetValue(layer, out LayerInfo info) || info.Learned == null ||
                !info.Learned.TryGetValue((mask << 8) | variant, out Rect[] quarters))
            {
                return false;
            }

            uv = quarters[corner];
            return true;
        }

        /// <summary>
        /// A quarter sprite (8x8) from the sub-tile lookup, for a quadrant whose sub-corner mask is
        /// <paramref name="subMask"/> — how CK composites the masks the 9-way table doesn't author
        /// (corners/T-junctions/end-caps are baked from these quarters).
        /// </summary>
        public static bool TryGetSubtileUV(LayerName layer, int subMask, int random, out Rect uv)
        {
            uv = default;
            Build();
            return layers.TryGetValue(layer, out LayerInfo info) && Lookup(info.Sub, subMask, random, out uv);
        }

        private static bool Lookup(Adaptive d, int dirFlags, int random, out Rect uv)
        {
            uv = default;
            if (d == null)
            {
                return false;
            }

            int masked = dirFlags & d.DirBits;
            if (masked < 0 || masked > 255)
            {
                return false;
            }

            int len = d.Length[masked];
            if (len <= 0)
            {
                return false;
            }

            int idx = d.Start[masked] + (random < 0 ? -random : random) % len;
            if (idx < 0 || idx >= d.Coords.Length)
            {
                return false;
            }

            uv = d.Coords[idx];
            return true;
        }

        /// <summary>A RandomFill layer's sprite, chosen by <paramref name="random"/>.</summary>
        public static bool TryGetRandomUV(LayerName layer, int random, out Rect uv)
        {
            uv = default;
            Build();
            if (!layers.TryGetValue(layer, out LayerInfo info) || info.Random == null || info.Random.Length == 0)
            {
                return false;
            }

            uv = info.Random[(random < 0 ? -random : random) % info.Random.Length];
            return true;
        }

        /// <summary>A layer's sprite by whichever fill it uses (adaptive first, then random).</summary>
        public static bool TryGetSpriteUV(LayerName layer, int dirFlags, int random, out Rect uv)
        {
            return TryGetAdaptiveUV(layer, dirFlags, random, out uv) || TryGetRandomUV(layer, random, out uv);
        }

        /// <summary>
        /// Core Keeper's exact position-seeded variant index — <c>new Random(math.hash(pos)).NextInt(0,256)</c>
        /// from <c>PugMapLayer2</c>, used as <c>index % variantCount</c>. It is DETERMINISTIC per tile
        /// position (a proper 2D hash, so no diagonal banding), which is why a patterned tileset lays down
        /// its intended pieces in the intended places rather than a per-placement roll. Pass this as the
        /// <c>random</c> argument to the UV lookups so the preview matches the game 1:1.
        /// </summary>
        public static int PositionVariant(int x, int z)
        {
            uint h = math.hash(new int2(x, z));
            return new Unity.Mathematics.Random(h == 0u ? 1u : h).NextInt(0, 256);
        }

        private static void Build()
        {
            if (built)
            {
                return;
            }

            built = true;
            try
            {
                string encoded = DimensionTilesetAtlasData.Encoded;
                if (string.IsNullOrWhiteSpace(encoded))
                {
                    return;
                }

                foreach (string raw in encoded.Split('\n'))
                {
                    ParseLine(raw.Trim().TrimEnd('\r'));
                }

                string gen = DimensionTilesetAtlasData.GenEncoded;
                if (!string.IsNullOrWhiteSpace(gen))
                {
                    foreach (string raw in gen.Split('\n'))
                    {
                        ParseLine(raw.Trim().TrimEnd('\r'));
                    }
                }

                string learned = DimensionTilesetAtlasData.LearnedEncoded;
                if (!string.IsNullOrWhiteSpace(learned))
                {
                    foreach (string raw in learned.Split('\n'))
                    {
                        ParseLine(raw.Trim().TrimEnd('\r'));
                    }
                }
            }
            catch (Exception)
            {
                // Malformed bake ⇒ no data; the preview keeps its fallback.
            }
        }

        private static LayerInfo Info(LayerName layer)
        {
            if (!layers.TryGetValue(layer, out LayerInfo info))
            {
                info = new LayerInfo { Layer = layer };
                layers[layer] = info;
            }

            return info;
        }

        private static void ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line) || line.Length < 2 || line[1] != ';')
            {
                return;
            }

            char kind = line[0];
            string[] parts = line.Split(';');
            if (parts.Length < 2 || !Enum.TryParse(parts[1], out LayerName layer))
            {
                return;
            }

            switch (kind)
            {
                case 'C':
                    ParseConfig(layer, parts);
                    break;
                case 'S':
                    Info(layer).Std = BuildAdaptive(parts);
                    break;
                case 'B':
                    Info(layer).Sub = BuildAdaptive(parts);
                    break;
                case 'A':
                    ParseRandom(layer, parts);
                    break;
                case 'G':
                    BuildGen(layer, parts);
                    break;
                case 'Q':
                    BuildLearned(layer, parts);
                    break;
            }
        }

        // Q;<name>;<templateW>;<templateH>;<entries> — the learned composited-mask quarter recipe. Each
        // entry is mask:variant:tlx:tly:trx:try:blx:bly:brx:bry, pixel coords (image order, y down) of the
        // four 8x8 quarters' top-lefts in the source-template sheet. Stored as y-up UV rects so the
        // existing UV→pixel extraction (round of uv×sheetSize) lands back on the exact template pixel.
        private static void BuildLearned(LayerName layer, string[] p)
        {
            if (p.Length < 5)
            {
                return;
            }

            float tw = ParseInt(p[2]);
            float th = ParseInt(p[3]);
            if (tw <= 0f || th <= 0f)
            {
                return;
            }

            sourceW = (int)tw;
            sourceH = (int)th;

            var learned = new Dictionary<int, Rect[]>();
            foreach (string entry in p[4].Split(','))
            {
                string[] t = entry.Split(':');
                if (t.Length != 10)
                {
                    continue;
                }

                int mask = ParseInt(t[0]);
                int variant = ParseInt(t[1]);
                if (mask < 0 || mask > 255 || variant < 0 || variant > 255)
                {
                    continue;
                }

                var quarters = new Rect[4];
                for (int c = 0; c < 4; c++)
                {
                    int px = ParseInt(t[2 + c * 2]);
                    int py = ParseInt(t[3 + c * 2]);
                    quarters[c] = new Rect(px / tw, (th - py - 8f) / th, 8f / tw, 8f / th);
                }

                learned[(mask << 8) | variant] = quarters;
            }

            if (learned.Count > 0)
            {
                Info(layer).Learned = learned;
            }
        }

        // G;<name>;<dirBits>;<W>;<H>;<coords>;<combos> — vanilla's baked GEN packing + its texture size.
        private static void BuildGen(LayerName layer, string[] p)
        {
            if (p.Length < 7)
            {
                return;
            }

            LayerInfo info = Info(layer);
            info.GenW = ParseInt(p[3]);
            info.GenH = ParseInt(p[4]);
            info.Gen = new Adaptive
            {
                DirBits = ParseInt(p[2]),
                Coords = ParseRects(p[5]),
                Start = new int[256],
                Length = new int[256],
            };
            foreach (string combo in p[6].Split(','))
            {
                string[] t = combo.Split(':');
                if (t.Length != 3)
                {
                    continue;
                }

                int df = ParseInt(t[0]);
                if (df >= 0 && df < 256)
                {
                    info.Gen.Start[df] = ParseInt(t[1]);
                    info.Gen.Length[df] = ParseInt(t[2]);
                }
            }
        }

        // C;<name>;<fill>;<faces>;<offY>;<hStretch>;<target>;<data>;<emis>
        private static void ParseConfig(LayerName layer, string[] p)
        {
            if (p.Length < 9)
            {
                return;
            }

            LayerInfo info = Info(layer);
            info.Fill = ParseInt(p[2]);
            info.Face = FaceFrom(p[3]);
            info.OffY = ParseFloat(p[4]);
            info.HStretch = ParseFloat(p[5]);
            info.HasTarget = Enum.TryParse(p[6], out LayerName target);
            info.Target = target;
            info.IsDataLayer = Enum.TryParse(p[7], out LayerName data) && p[7] != "none";
            info.Data = data;
            info.Emissive = p[8] == "1";
        }

        private static FaceKind FaceFrom(string faces)
        {
            if (string.IsNullOrEmpty(faces))
            {
                return FaceKind.Top;
            }

            bool top = false;
            foreach (string f in faces.Split(','))
            {
                int v = ParseInt(f);
                if (v >= 2 && v <= 5)
                {
                    return FaceKind.Side;
                }

                if (v == 0)
                {
                    top = true;
                }
            }

            return top ? FaceKind.Top : FaceKind.Bottom;
        }

        // S/B;<name>;<dirBits>;<coords>;<combos>
        private static Adaptive BuildAdaptive(string[] p)
        {
            if (p.Length < 5)
            {
                return null;
            }

            Rect[] coords = ParseRects(p[3]);
            int[] start = new int[256];
            int[] length = new int[256];
            foreach (string combo in p[4].Split(','))
            {
                string[] t = combo.Split(':');
                if (t.Length != 3)
                {
                    continue;
                }

                int df = ParseInt(t[0]);
                if (df >= 0 && df < 256)
                {
                    start[df] = ParseInt(t[1]);
                    length[df] = ParseInt(t[2]);
                }
            }

            return new Adaptive { DirBits = ParseInt(p[2]), Coords = coords, Start = start, Length = length };
        }

        // A;<name>;<x,y,w,h,...>
        private static void ParseRandom(LayerName layer, string[] p)
        {
            if (p.Length < 3)
            {
                return;
            }

            Info(layer).Random = ParseRects(p[2]);
        }

        private static Rect[] ParseRects(string csv)
        {
            if (string.IsNullOrEmpty(csv))
            {
                return Array.Empty<Rect>();
            }

            string[] f = csv.Split(',');
            int n = f.Length / 4;
            Rect[] rects = new Rect[n];
            for (int i = 0; i < n; i++)
            {
                rects[i] = new Rect(ParseFloat(f[i * 4]), ParseFloat(f[i * 4 + 1]), ParseFloat(f[i * 4 + 2]), ParseFloat(f[i * 4 + 3]));
            }

            return rects;
        }

        private static int ParseInt(string s)
        {
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
        }

        private static float ParseFloat(string s)
        {
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }
    }
}
