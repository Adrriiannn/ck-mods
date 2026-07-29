using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PugTilemap;
using PugTilemap.Quads;
using PugTilemap.Workshop;
using UnityEngine;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// Runs in-game and LOGS the ENTIRE structure of Core Keeper's dirt tileset to the player log — every
    /// layer (ground, wall, their vertical <c>…Front</c> sides, plus watered/flooded/slime/pebbles/grass/
    /// roots/debris/cracks/roof-holes/sun-beam/etc.), each with its full geometry, faces, fill type,
    /// per-state sprite lists and adaptive tables. The mod SDK can't see this at edit time because the
    /// <c>MapWorkshopTilesetBank</c> asset isn't shipped; in-game the bank is loaded, so we enumerate
    /// <c>PugMapTileset.layers</c> directly (no guess-list) and dump it. Marker <c>[NF_ATLAS]</c>; the mod
    /// sandbox forbids System.IO, so this uses Debug.Log rather than writing a file. One-time.
    ///
    /// Line formats (all after the <c>[NF_ATLAS]</c> marker):
    ///   TEX name:WxH        — the bank's packed tileset texture
    ///   MAINTEX name:WxH    — the tileset's source sheet (what modders author against)
    ///   LAYERS n
    ///   LAYER  name;data=..;target=..;dontAdapt=..;fill=<int>;fullAdaptive=0/1;onlyOwn=0/1;off=x,y,z;
    ///          hStretch=..;pad=..;skew=0/1;faces=<int,int,..>;quad=WxH;ignDVH=<3 bits>;walkable=0/1;
    ///          dataLayer=0/1;ceilLight=0/1;emissive=name:WxH|-;customTex=name:WxH|-;hideInterior=0/1;
    ///          hideUnderWall=0/1;unityLayer=..;sortLayer=..
    ///   AUV    name;s0count,x,y,w,h,..|s1count,..|s2count,..     — allSpriteUVs per state (RandomFill etc.)
    ///   STD    name;dirBits;x,y,w,h,..;df:start:len,..           — 9-way adaptive lookup (source-sheet UVs)
    ///   SUB    name;dirBits;x,y,w,h,..;df:start:len,..           — sub-tile adaptive lookup (CK's own)
    /// FillType: 0 NoFill,1 RandomFill,2 AdaptativeFill,3 AdaptativeExtrude,4 CustomFill,5 RandomFillEditorOnly.
    /// TileFace: 0 TOP,1 BOTTOM,2 FRONT,3 BACK,4 LEFT,5 RIGHT.
    /// </summary>
    public static class DimensionTilesetAtlasCapture
    {
        public const string Marker = "[NF_ATLAS]";
        private static bool done;

        /// <summary>Idempotent; retries each call until the bank + its baked lookup are ready, then logs once.</summary>
        public static void TryCaptureOnce()
        {
            if (done)
            {
                return;
            }

            try
            {
                MapWorkshopTilesetBank bank =
                    Resources.Load<MapWorkshopTilesetBank>("MapWorkshop/MapWorkshopTilesetBank");
                if (bank == null || bank.tilesets == null || bank.tilesets.Count == 0)
                {
                    return;
                }

                PugMapTileset dirt = bank.tilesets[0] != null ? bank.tilesets[0].layers : null;
                if (dirt == null || dirt.layers == null || dirt.layers.Count == 0)
                {
                    return;
                }

                QuadGenerator ground = dirt.GetDef(LayerName.ground);
                if (ground == null || !HasData(Pick(ground)))
                {
                    return; // baked lookup not populated yet — retry next frame
                }

                done = true;
                Debug.Log(Marker + "BEGIN");

                // Dirt (index 0) is the primary donor; oasis (index 66) is the richest vanilla
                // tileset (multi-slime/ore/grass states) and becomes the second donor. Each block is
                // bracketed by TSET markers so the bake tooling can split them.
                DumpTileset(bank, 0);
                DumpTileset(bank, 66);

                // Which vanilla tilesets are rigid, and on which layers. Compact on purpose — the
                // full per-tileset dump above is enormous, and all this question needs is the flag.
                for (int i = 0; i < bank.tilesets.Count; i++)
                {
                    MapWorkshopTilesetBank.Tileset entry = bank.tilesets[i];
                    PugMapTileset entryLayers = entry != null ? entry.layers : null;
                    if (entryLayers == null || entryLayers.layers == null)
                    {
                        continue;
                    }

                    string rigidLayers = string.Empty;
                    for (int l = 0; l < entryLayers.layers.Count; l++)
                    {
                        QuadGenerator def = entryLayers.layers[l];
                        if (def == null || !def.layerIgnoresVertexOffsets)
                        {
                            continue;
                        }

                        rigidLayers += (rigidLayers.Length > 0 ? "," : string.Empty) + def.layerName;
                    }

                    Debug.Log(
                        Marker + "RIGID " + i + ";" + (entry.friendlyName ?? string.Empty) +
                        ";template=" + entryLayers.name +
                        ";rigid=" + (rigidLayers.Length > 0 ? rigidLayers : "-"));
                }

                Debug.Log(Marker + "END");
            }
            catch (Exception e)
            {
                done = true;
                Debug.Log(Marker + "ERROR " + e.Message);
            }
        }

        // One tileset's complete dump: config + AUV (all states) + STD/SUB/GEN adaptive tables for
        // EVERY state (0..2 — the runtime indexes all of them by the tile's state; dumping only
        // state 0 was the root of the missing-art gap). State 0 keeps the bare prefix for
        // backward-compatible parsing; states 1+ get a numeric suffix (STD1, SUB2, ...).
        private static void DumpTileset(MapWorkshopTilesetBank bank, int index)
        {
            if (bank.tilesets == null || index < 0 || index >= bank.tilesets.Count || bank.tilesets[index] == null)
            {
                return;
            }

            PugMapTileset set = bank.tilesets[index].layers;
            if (set == null || set.layers == null || set.layers.Count == 0)
            {
                return;
            }

            Debug.Log(Marker + "TSET " + index + " begin");
            Texture2D packed = bank.tilesets[index].tilesetTextures != null ? bank.tilesets[index].tilesetTextures.texture : null;
            Debug.Log(Marker + "TEX " + TexInfo(packed));
            Debug.Log(Marker + "MAINTEX " + TexInfo(set.tilesetTexture));
            Debug.Log(Marker + "LAYERS " + set.layers.Count);

            foreach (QuadGenerator def in set.layers)
            {
                if (def == null)
                {
                    continue;
                }

                Debug.Log(Marker + "LAYER " + LayerConfig(def));

                string auv = AuvAllStates(def);
                if (auv.Length > 0)
                {
                    Debug.Log(Marker + "AUV " + def.layerName + ";" + auv);
                }

                DumpAdaptive("STD", def.layerName, def.adaptativeSpriteLookupTable, def.adaptativeDirBitsAvailable);
                DumpAdaptive("SUB", def.layerName, def.adaptativeSubtileSpriteLookupTable, def.adaptativeSubtileDirBitsAvailable);

                // GEN = the baked full-adaptive lookup: per-mask variant COUNT + each variant's exact cell in
                // the packed 256×320 sheet. This IS vanilla's canonical output layout (identical across all
                // tilesets) — capturing it lets the generator emit tilesets in Core Keeper's exact format.
                DumpAdaptive("GEN", def.layerName, def.generatedTextureAdaptativeSpriteLookupTable, def.generatedTextureAdaptativeDirBitsAvailable);
            }

            Debug.Log(Marker + "TSET " + index + " end");
        }

        // Every scalar knob of a layer, so its role (top cap / side / underground / overlay / light) and its
        // geometry (where above-ground vs underground sits) can be read without the asset.
        private static string LayerConfig(QuadGenerator def)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(def.layerName).Append(';');
            sb.Append("data=").Append(def.dataTile).Append(';');
            sb.Append("target=").Append(def.targetTile).Append(';');
            sb.Append("dontAdapt=").Append(def.dontAdaptIfTilePresent).Append(';');
            sb.Append("fill=").Append((int)def.meshFillType).Append(';');
            sb.Append("fullAdaptive=").Append(def.isUsingFullAdaptiveTexture ? 1 : 0).Append(';');
            sb.Append("onlyOwn=").Append(def.onlyAdaptToOwnTileset ? 1 : 0).Append(';');
            sb.Append("off=").Append(F(def.offset.x)).Append(',').Append(F(def.offset.y)).Append(',').Append(F(def.offset.z)).Append(';');
            sb.Append("hStretch=").Append(F(def.heightStretch)).Append(';');
            sb.Append("pad=").Append(F(def.padding)).Append(';');
            sb.Append("skew=").Append(def.skewInTopVertices ? 1 : 0).Append(';');
            sb.Append("faces=");
            if (def.tileFaces != null)
            {
                for (int i = 0; i < def.tileFaces.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append((int)def.tileFaces[i]);
                }
            }

            sb.Append(';');
            sb.Append("quad=").Append(def.quadSize.x).Append('x').Append(def.quadSize.y).Append(';');
            sb.Append("ignDVH=").Append(def.ignoreDiagonalQuads ? 1 : 0).Append(def.ignoreVerticalQuads ? 1 : 0).Append(def.ignoreHorizontalQuads ? 1 : 0).Append(';');
            sb.Append("walkable=").Append(def.isWalkable ? 1 : 0).Append(';');
            sb.Append("dataLayer=").Append(def.isDataLayer ? 1 : 0).Append(';');
            sb.Append("ceilLight=").Append(def.actAsCeilingLight ? 1 : 0).Append(';');
            sb.Append("emissive=").Append(TexInfo(def.emissiveTexture)).Append(';');
            sb.Append("customTex=").Append(TexInfo(def.customTexture)).Append(';');
            sb.Append("hideInterior=").Append(def.hideInteriorFaces ? 1 : 0).Append(';');
            sb.Append("hideUnderWall=").Append(def.hideUnderNonTransparentWall ? 1 : 0).Append(';');
            sb.Append("unityLayer=").Append(def.layer).Append(';');
            sb.Append("sortLayer=").Append(def.sortingLayer).Append(';');
            // Gates which of the two cap-trim vertex variants the runtime mesh builder uses
            // (PugMapLayer2 ~:1014 vs ~:1032) — needed to replicate wall-top squash 1:1.
            sb.Append("ignVtxOff=").Append(def.layerIgnoresVertexOffsets ? 1 : 0);
            return sb.ToString();
        }

        // allSpriteUVs for all 3 states, "s0count,rects|s1count,rects|s2count,rects".
        private static string AuvAllStates(QuadGenerator def)
        {
            if (def.allSpriteUVs == null)
            {
                return string.Empty;
            }

            bool any = false;
            StringBuilder sb = new StringBuilder();
            for (int s = 0; s < def.allSpriteUVs.Length; s++)
            {
                if (s > 0)
                {
                    sb.Append('|');
                }

                QuadGenerator.SpriteUVS state = def.allSpriteUVs[s];
                if (state == null || state.spriteUVS == null || state.spriteUVS.Count == 0)
                {
                    sb.Append('0');
                    continue;
                }

                any = true;
                sb.Append(state.spriteUVS.Count);
                for (int i = 0; i < state.spriteUVS.Count; i++)
                {
                    Rect r = state.spriteUVS[i];
                    sb.Append(',').Append(F(r.x)).Append(',').Append(F(r.y)).Append(',').Append(F(r.width)).Append(',').Append(F(r.height));
                }
            }

            return any ? sb.ToString() : string.Empty;
        }

        private static void DumpAdaptive(string prefix, LayerName layer, AdaptativeSpriteLookupTable[] arr, byte[] dirBits)
        {
            if (arr == null || arr.Length == 0)
            {
                return;
            }

            // Every state — the runtime picks the table by the tile's state (crack levels, slime/ore
            // variants), so a state-0-only dump loses real, consumed data.
            for (int s = 0; s < arr.Length; s++)
            {
                AdaptativeSpriteLookupTable lut = arr[s];
                if (!HasData(lut))
                {
                    continue;
                }

                byte bits = dirBits != null && dirBits.Length > s ? dirBits[s]
                    : dirBits != null && dirBits.Length > 0 ? dirBits[0] : (byte)255;
                string tag = s == 0 ? prefix : prefix + s;
                Debug.Log(Marker + tag + " " + Serialize(layer, bits, lut));
            }
        }

        private static string TexInfo(Texture2D t)
        {
            return t != null ? t.name + ":" + t.width + "x" + t.height : "-";
        }

        private static AdaptativeSpriteLookupTable Pick(QuadGenerator def)
        {
            AdaptativeSpriteLookupTable[] arr = def.isUsingFullAdaptiveTexture
                ? def.generatedTextureAdaptativeSpriteLookupTable
                : def.adaptativeSpriteLookupTable;
            return arr != null && arr.Length > 0 ? arr[0] : null;
        }

        private static bool HasData(AdaptativeSpriteLookupTable lut)
        {
            return lut != null && lut.allSpriteCoords != null && lut.allSpriteCoords.Length > 0
                && lut.sublistStart != null && lut.sublistStart.Length >= 256
                && lut.sublistLength != null && lut.sublistLength.Length >= 256;
        }

        // "layer;dirBits;x,y,w,h,…;df:start:len,…" (only non-empty direction masks).
        private static string Serialize(LayerName layer, byte bits, AdaptativeSpriteLookupTable lut)
        {
            StringBuilder coords = new StringBuilder();
            for (int i = 0; i < lut.allSpriteCoords.Length; i++)
            {
                Rect r = lut.allSpriteCoords[i];
                if (i > 0)
                {
                    coords.Append(',');
                }

                coords.Append(F(r.x)).Append(',').Append(F(r.y)).Append(',').Append(F(r.width)).Append(',').Append(F(r.height));
            }

            StringBuilder combos = new StringBuilder();
            bool first = true;
            for (int df = 0; df < 256; df++)
            {
                if (lut.sublistLength[df] <= 0)
                {
                    continue;
                }

                if (!first)
                {
                    combos.Append(',');
                }

                first = false;
                combos.Append(df).Append(':').Append(lut.sublistStart[df]).Append(':').Append(lut.sublistLength[df]);
            }

            return layer + ";" + bits + ";" + coords + ";" + combos;
        }

        private static string F(float v)
        {
            return v.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
