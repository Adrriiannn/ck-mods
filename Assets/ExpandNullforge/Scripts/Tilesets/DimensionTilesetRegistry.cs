using System;
using System.Collections.Generic;
using PugTilemap;
using PugTilemap.Quads;
using PugTilemap.Workshop;
using UnityEngine;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// Registry of custom tilesets, keyed by their derived numeric ids.
    ///
    /// Identity model: a custom tileset's id is a deterministic hash of its string name — a pure
    /// function, not an allocation. There is no allocator whose answers could differ between
    /// sessions, so the bare ids stored inside world saves (TileCD.tileset in the game's chunk
    /// data) remain correct across any change of mod set, load order or machine. Vanilla ids
    /// (0–74, enum-frozen) and CoreLib's reserved band (100–199) are both far below
    /// <see cref="MinCustomTilesetId"/>, so nothing else can mint our numbers.
    ///
    /// Tiles whose id has no registration (their mod was removed) render through an explicit
    /// magenta "missing" placeholder — never another tileset's skin, never a crash — and restore
    /// pixel-perfect when the mod returns.
    /// </summary>
    public static class DimensionTilesetRegistry
    {
        /// <summary>
        /// Lowest custom tileset id. Leaves vanilla (0–74) generous growth headroom and stays
        /// clear of CoreLib's reserved 100–199 band.
        /// </summary>
        public const int MinCustomTilesetId = 1000;

        /// <summary>
        /// One past the highest custom id. The full positive <c>int</c> range, because
        /// <c>TileCD.tileset</c> — the field ids actually live in at runtime, in world saves and
        /// over the wire — is a plain <c>int</c> (<c>Pug.Base/TileCD.cs:45</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS WAS 65536 AND THAT WAS TOO SMALL. The 16-bit band was kept so ids could round-trip
        /// through <c>PugmapTileData.tilesetType</c>, a <c>ushort</c> in the authored-map format
        /// (<c>PugMap.Common/PugmapTileData.cs:34</c>), in case a future scene system wanted to
        /// store tiles in the vanilla format without a remap step. The price was collision odds,
        /// and the price turned out to be far higher than "a few percent":
        /// </para>
        /// <code>
        ///   tilesets   16-bit band     this range
        ///        30        0.67%      0.0000203%
        ///       100        7.39%      0.0002305%
        ///       200       26.56%      0.0009267%
        ///       300       50.14%      0.0020885%
        ///      1000       99.96%      0.0232571%
        /// </code>
        /// <para>
        /// A shared ecosystem reaches 200 tilesets across a handful of mods easily, and a one-in-four
        /// chance that two of them cannot coexist is not a foundation to build on. A hypothetical
        /// future convenience does not justify it.
        /// </para>
        /// <para>
        /// WHAT THIS COSTS. If a scene format ever does store tiles as <c>PugmapTileData</c>, it must
        /// map our ids to a per-scene 16-bit index at bake time rather than casting — an unchecked
        /// <c>(ushort)</c> cast of an id above 65535 would silently alias it to a different tileset.
        /// That remap is local to whatever writes scenes; it is not a constraint on identity. Vanilla
        /// tooling is unlikely to walk into it on its own: the Map Workshop paints from the
        /// index-addressed <c>MapWorkshopTilesetBank.tilesets</c> list, which has no entry for a
        /// custom id, and its fill path writes <c>pugMapLayer.tilesetKey</c> — a key that came from a
        /// bank-backed layer (<c>Pug.Other/PugTilemap/Workshop/Fill.cs:135</c>).
        /// </para>
        /// <para>
        /// Stated as "unlikely" rather than "cannot", deliberately. The EXPLICIT <c>(ushort)</c> casts
        /// are enumerable and were enumerated. The IMPLICIT <c>TileInfo</c>→<c>PugmapTileData</c>
        /// operator is not: the compiler inserts it with no distinguishing call-site text, so its
        /// absence from a code path cannot be established by searching — only its presence can. The
        /// dictionary records that same question as unresolved for the same reason. Treat "nothing
        /// converts through it" as an unproven assumption, not a verified fact.
        /// </para>
        /// </remarks>
        public const int MaxCustomTilesetIdExclusive = int.MaxValue;

        private static readonly Dictionary<int, DimensionCustomTileset> TilesetsById =
            new Dictionary<int, DimensionCustomTileset>();

        private static readonly Dictionary<string, DimensionCustomTileset> TilesetsByName =
            new Dictionary<string, DimensionCustomTileset>(StringComparer.Ordinal);

        private static readonly HashSet<int> WarnedUnknownIds = new HashSet<int>();

        private static Texture2D missingTexture;
        private static Texture2D blankTexture;
        private static readonly HashSet<int> WarnedMissingAdaptiveLayers = new HashSet<int>();
        private static readonly HashSet<int> WarnedGuardLifts = new HashSet<int>();
        private static MapWorkshopTilesetBank.TilesetTextures missingTextures;
        private static PugMapTileset fallbackLayers;

        /// <summary>
        /// The deterministic name → id mapping: 64-bit FNV-1a folded into
        /// [<see cref="MinCustomTilesetId"/>, <see cref="MaxCustomTilesetIdExclusive"/>). Same name
        /// ⇒ same id on every install, forever. This is the identity that makes saved tiles immune
        /// to load order and mod-set changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 64-BIT, NOT 32. The output range is ~2^31, so a 32-bit hash would leave the modulo with
        /// two preimages per id and a ragged tail where ~2M ids get three — a 1.5× weighting on
        /// 0.09% of the space. Harmless in isolation, but it is a bias with no upside: 64-bit FNV-1a
        /// costs one extra multiply per character and drops the worst-case weighting to 1 + 2^-33.
        /// The stronger avalanche also matters for the inputs we actually get, which are short and
        /// share long prefixes (<c>MyMod:stone</c>, <c>MyMod:stone_dark</c>) — exactly the shape
        /// 32-bit FNV mixes least well.
        /// </para>
        /// <para>
        /// Hashing runs over UTF-16 code units, low byte first, so it needs no encoding pass and no
        /// allocation, and cannot vary with the machine's culture. It is not byte-wise FNV over
        /// UTF-8 and does not need to be — nothing outside this framework recomputes these ids. What
        /// it must be is stable, which it is: the loop depends on nothing but the characters.
        /// </para>
        /// <para>
        /// CHANGING THIS FUNCTION RENUMBERS EVERY TILESET, orphaning already-placed tiles in existing
        /// worlds. Treat it as a save-format decision, not an implementation detail.
        /// </para>
        /// </remarks>
        public static int ComputeTilesetId(string name)
        {
            // The hash itself is ExpandNullforge.Core.DimensionFnv, with the biome id and the layout
            // fingerprint, because all three are saved into worlds and a second copy of the walk is
            // a second chance for a saved id to stop matching its own name. The three copies this
            // replaced were compared over every character in the basic multilingual plane and over
            // 200,000 random strings and agreed on all of them, so no id computed before this
            // computes differently now.
            ulong hash = ExpandNullforge.Core.DimensionFnv.Hash(name);

            ulong range = (ulong)(MaxCustomTilesetIdExclusive - MinCustomTilesetId);
            return MinCustomTilesetId + (int)(hash % range);
        }

        /// <summary>
        /// Registers (or replaces, for the same name) a custom tileset. A hash collision between
        /// two DIFFERENT names is rejected loudly. Returns true when the tileset is live.
        /// </summary>
        /// <remarks>
        /// DO NOT "FIX" THIS WITH PROBING. Resolving a collision by walking to the next free id
        /// would look like an improvement and would quietly destroy the one property the whole
        /// scheme rests on: that a name maps to an id by itself, with no reference to what else is
        /// installed. Under probing, which of two colliding tilesets keeps the base id depends on
        /// registration order, so a player adding or removing an unrelated mod could renumber a
        /// tileset they had already built with — turning every placed tile into a different block.
        /// Rejection keeps the damage to one tileset, visibly, at load. Renaming is the fix.
        /// </remarks>
        public static bool Register(DimensionCustomTileset tileset)
        {
            if (tileset == null || string.IsNullOrEmpty(tileset.Name))
            {
                return false;
            }

            if (TilesetsById.TryGetValue(tileset.Id, out DimensionCustomTileset existing))
            {
                if (!string.Equals(existing.Name, tileset.Name, StringComparison.Ordinal))
                {
                    // Which one wins is load order, so name both — the reader may own neither, and
                    // needs to know who to tell.
                    DimensionLog.Fatal(DimensionLogChannels.Tileset, null, 
                        "Custom tileset id collision: '" + tileset.Name +
                        "' and '" + existing.Name + "' both derive id " + tileset.Id +
                        ". '" + existing.Name + "' registered first and stays active; '" +
                        tileset.Name + "' will render as a missing tileset. The fix is for the " +
                        "author of either one to rename their tileset id — the odds of this are " +
                        "about 1 in 5 million for a 30-tileset mod, so it is worth reporting.");
                    return false;
                }

                // Same tileset re-announced (ModObjectLoaded can fire more than once): keep the
                // latest textures/colors but stay quiet — the first registration already logged.
                TilesetsById[tileset.Id] = tileset;
                TilesetsByName[tileset.Name] = tileset;
                return true;
            }

            TilesetsById[tileset.Id] = tileset;
            TilesetsByName[tileset.Name] = tileset;
            DimensionLog.Trace(DimensionLogChannels.Tileset, null, 
                "Custom tileset registered: " + tileset.Name +
                " -> id " + tileset.Id + ".");
            return true;
        }

        public static bool TryGet(int tilesetId, out DimensionCustomTileset tileset)
        {
            return TilesetsById.TryGetValue(tilesetId, out tileset);
        }

        public static bool TryGetByName(string name, out DimensionCustomTileset tileset)
        {
            tileset = null;
            return !string.IsNullOrEmpty(name) && TilesetsByName.TryGetValue(name, out tileset);
        }

        // ---- Vanilla reskin overrides ----
        // vanillaIndex (0-74) → the custom tileset whose TEXTURES that index should render with.
        // Render-only and save-safe: placed tiles keep their vanilla id, so removing the override
        // restores vanilla pixels exactly. Layer/UV rules stay vanilla's own — only textures swap.
        private static readonly Dictionary<int, DimensionCustomTileset> ReskinsByVanillaIndex =
            new Dictionary<int, DimensionCustomTileset>();

        /// <summary>
        /// Registers a reskin: while present, the vanilla tileset at <paramref name="vanillaIndex"/>
        /// renders with <paramref name="tileset"/>'s textures. Global per index (every tile of that
        /// tileset, in every world). Last registration wins; a warning is logged on overwrite so
        /// clashing mods/blocks are visible.
        /// </summary>
        public static bool RegisterReskin(int vanillaIndex, DimensionCustomTileset tileset)
        {
            if (tileset == null || vanillaIndex < 0 || vanillaIndex >= 75)
            {
                return false;
            }

            if (ReskinsByVanillaIndex.TryGetValue(vanillaIndex, out DimensionCustomTileset existing) &&
                existing != null && !string.Equals(existing.Name, tileset.Name, StringComparison.Ordinal))
            {
                DimensionLog.Problem(DimensionLogChannels.Tileset, null, 
                    "Vanilla tileset index " + vanillaIndex + " is reskinned twice ('" +
                    existing.Name + "' then '" + tileset.Name + "') — the last one wins.");
            }

            ReskinsByVanillaIndex[vanillaIndex] = tileset;
            return true;
        }

        /// <summary>The reskin serving a vanilla tileset index, if any.</summary>
        public static bool TryGetReskin(int vanillaIndex, out DimensionCustomTileset tileset)
        {
            return ReskinsByVanillaIndex.TryGetValue(vanillaIndex, out tileset) && tileset != null;
        }

        /// <summary>All registered custom tilesets (for the color-table patch and diagnostics).</summary>
        public static IReadOnlyCollection<DimensionCustomTileset> All => TilesetsById.Values;

        /// <summary>Ids at or above <see cref="MinCustomTilesetId"/> belong to this scheme.</summary>
        public static bool IsCustomTilesetId(int tilesetId)
        {
            return tilesetId >= MinCustomTilesetId;
        }

        /// <summary>
        /// One warning per unknown id per session: tells the player WHICH tiles are orphaned
        /// (their authoring mod is not installed) instead of leaving anonymous magenta squares.
        /// </summary>
        public static void WarnUnknownIdOnce(int tilesetId)
        {
            if (WarnedUnknownIds.Add(tilesetId))
            {
                DimensionLog.Problem(DimensionLogChannels.Tileset, null, 
                    "This world contains tiles from an unregistered custom " +
                    "tileset (id " + tilesetId + "). The mod that authored them is probably not " +
                    "installed; the tiles render as the magenta placeholder and restore when it returns.");
            }
        }

        /// <summary>Friendly name for missing ids (paint tool, tooltips, logs).</summary>
        public static string GetMissingFriendlyName(int tilesetId)
        {
            return "Missing tileset (" + tilesetId + ")";
        }

        /// <summary>
        /// The rule set used to render a custom tileset with no own LayersTemplate, and any
        /// unknown id: the vanilla Dirt rules — structurally correct for standard ground/wall
        /// tilesets, with textures swapped by the texture patches. Null until the game's bank
        /// has loaded (nothing can be rendering tiles before that).
        /// </summary>
        public static PugMapTileset ResolveFallbackLayers()
        {
            if (fallbackLayers != null)
            {
                return fallbackLayers;
            }

            try
            {
                List<MapWorkshopTilesetBank.Tileset> tilesets = TilesetTypeUtility.GetTilesets();
                if (tilesets != null && tilesets.Count > 0 && tilesets[0] != null)
                {
                    fallbackLayers = tilesets[0].layers;
                }
            }
            catch (Exception)
            {
                // Bank not loaded yet — callers treat null as "let vanilla handle it".
            }

            return fallbackLayers;
        }

        /// <summary>The magenta/black checker used for unregistered ids.</summary>
        public static Texture2D GetMissingTexture()
        {
            if (missingTexture == null)
            {
                missingTexture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat,
                    name = "ExpandNullforge_MissingTileset"
                };
                Color32 magenta = new Color32(255, 0, 255, 255);
                Color32 black = new Color32(20, 0, 20, 255);
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        bool even = ((x / 4) + (y / 4)) % 2 == 0;
                        missingTexture.SetPixel(x, y, even ? (Color)magenta : (Color)black);
                    }
                }

                missingTexture.Apply(false, false);
            }

            return missingTexture;
        }

        /// <summary>
        /// Reports, once per tileset, that a custom tile write got past vanilla's tileset-range guard
        /// on this peer. The single most useful line when a placed block does not stick.
        /// </summary>
        public static void LogGuardLiftOnce(int tilesetId)
        {
            if (!WarnedGuardLifts.Add(tilesetId))
            {
                return;
            }

            TryGet(tilesetId, out DimensionCustomTileset tileset);
            DimensionLog.Trace(DimensionLogChannels.Tileset, null, 
                "Writing custom tiles for '" +
                (tileset != null ? tileset.Name : "unknown id " + tilesetId) + "' (id " + tilesetId + ").");
        }

        /// <summary>
        /// Reports, once per tileset+layer, that a full-adaptive layer had no baked sheet. This is
        /// always an authoring bug — regenerate the tileset — and it is otherwise invisible, because
        /// the layer simply renders blank.
        /// </summary>
        public static void WarnMissingAdaptiveLayerOnce(int tilesetId, PugTilemap.LayerName layer)
        {
            int key = (tilesetId * 397) ^ (int)layer;
            if (!WarnedMissingAdaptiveLayers.Add(key))
            {
                return;
            }

            TryGet(tilesetId, out DimensionCustomTileset tileset);
            DimensionLog.Problem(DimensionLogChannels.Tileset, null, 
                "Tileset '" + (tileset != null ? tileset.Name : tilesetId.ToString()) +
                "' has no baked sheet for the full-adaptive '" + layer +
                "' layer, so it renders blank. Regenerate the tileset in the Tileset Studio.");
        }

        /// <summary>
        /// A fully transparent sheet handed to full-adaptive layers a custom tileset never baked.
        /// Returning null there is not safe: the tile renderer treats a null adaptive texture as
        /// "use the layer template's own texture" (PugMapLayer2.Init2), and because a custom tileset
        /// borrows vanilla's shared layer template, that texture belongs to an unrelated tileset —
        /// so the layer paints itself with fragments of someone else's art instead of drawing
        /// nothing. Transparent is the honest answer for a layer with no art.
        /// </summary>
        public static Texture2D GetBlankTexture()
        {
            if (blankTexture == null)
            {
                blankTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    name = "ExpandNullforge_BlankLayer"
                };
                blankTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
                blankTexture.Apply();
            }

            return blankTexture;
        }

        public static MapWorkshopTilesetBank.TilesetTextures GetMissingTextures()
        {
            if (missingTextures == null)
            {
                missingTextures = new MapWorkshopTilesetBank.TilesetTextures
                {
                    texture = GetMissingTexture()
                };
            }

            return missingTextures;
        }

        public static void Clear()
        {
            TilesetsById.Clear();
            TilesetsByName.Clear();
            ReskinsByVanillaIndex.Clear();
            WarnedUnknownIds.Clear();
            // The generated textures/fallback survive on purpose — they are session assets.
        }
    }
}
