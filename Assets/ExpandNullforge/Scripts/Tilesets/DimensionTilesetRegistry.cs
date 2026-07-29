using System;
using System.Collections.Generic;
using PugTilemap;
using PugTilemap.Quads;
using PugTilemap.Workshop;
using UnityEngine;

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
        /// Custom ids stop below 65536 because Core Keeper's prefab-map format stores a tile's tileset
        /// as a <c>ushort</c> with an unchecked cast (<c>PugmapTileData.tilesetType</c>). Anything above
        /// that is silently truncated when a tile is saved into a prefab map or custom scene, so the
        /// block comes back as a different tileset entirely. World saves and multiplayer both carry the
        /// full 32-bit value, so this ceiling exists purely for the scene format — but scenes are how
        /// handcrafted structures ship, which makes it load-bearing.
        /// </summary>
        public const int MaxCustomTilesetIdExclusive = 65536;

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
        /// The deterministic name → id mapping (FNV-1a folded into [MinCustomTilesetId,
        /// int.MaxValue)). Same name ⇒ same id on every install, forever. This is the identity
        /// that makes saved tiles immune to load order and mod-set changes.
        /// </summary>
        public static int ComputeTilesetId(string name)
        {
            uint hash = 2166136261u;
            if (!string.IsNullOrEmpty(name))
            {
                for (int i = 0; i < name.Length; i++)
                {
                    hash ^= name[i];
                    hash *= 16777619u;
                }
            }

            uint range = (uint)(MaxCustomTilesetIdExclusive - MinCustomTilesetId);
            return MinCustomTilesetId + (int)(hash % range);
        }

        /// <summary>
        /// Registers (or replaces, for the same name) a custom tileset. A hash collision between
        /// two DIFFERENT names is rejected loudly — the fix is renaming one tileset id, and the
        /// odds at 31 bits are negligible. Returns true when the tileset is live.
        /// </summary>
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
                    Debug.LogError(
                        "[ExpandNullforge] Custom tileset id collision: '" + tileset.Name +
                        "' and '" + existing.Name + "' both derive id " + tileset.Id +
                        ". Rename one tileset id to resolve; the earlier registration stays active.");
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
            Debug.Log(
                "[ExpandNullforge] Custom tileset registered: " + tileset.Name +
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
                Debug.LogWarning(
                    "[ExpandNullforge] Vanilla tileset index " + vanillaIndex + " is reskinned twice ('" +
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
                Debug.LogWarning(
                    "[ExpandNullforge] This world contains tiles from an unregistered custom " +
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
            Debug.Log(
                "[NF_TILESET] Writing custom tiles for '" +
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
            Debug.LogWarning(
                "[ExpandNullforge] Tileset '" + (tileset != null ? tileset.Name : tilesetId.ToString()) +
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
