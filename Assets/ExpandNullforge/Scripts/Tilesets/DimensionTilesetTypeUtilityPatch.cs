using HarmonyLib;
using PugTilemap;
using PugTilemap.Quads;
using PugTilemap.Workshop;
using UnityEngine;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// Serves custom tileset ids through the game's own tileset lookup hub.
    ///
    /// Vanilla resolves a tile's tileset number as a raw index into the shipped
    /// MapWorkshopTilesetBank list — anything outside it would throw. These prefixes intercept:
    /// ids below 75 (all of vanilla) always pass through untouched; registered custom ids
    /// (>= <see cref="DimensionTilesetRegistry.MinCustomTilesetId"/>) are served from the
    /// registry; any other id that would fall off the end of the bank renders as the explicit
    /// "missing tileset" placeholder instead of crashing — covering our own ids whose mod was
    /// removed AND stray ids from third-party schemes.
    ///
    /// Priority.Low keeps us cooperative with CoreLib: its tileset patches (reserved band
    /// 100–199) run first and serve their own ids; we only catch what nobody else claimed.
    /// </summary>
    [HarmonyPatch(typeof(TilesetTypeUtility))]
    internal static class DimensionTilesetTypeUtilityPatch
    {
        private const int VanillaMaxExclusive = 75;

        /// <summary>True when this id is ours to serve (registered, or orphaned/out-of-range).</summary>
        private static bool ShouldHandle(int index, out DimensionCustomTileset tileset)
        {
            if (DimensionTilesetRegistry.TryGet(index, out tileset))
            {
                return true;
            }

            if (index < VanillaMaxExclusive)
            {
                return false;
            }

            // Unknown high id: only claim it when the bank genuinely cannot (it would throw) —
            // an appended bank entry or another framework's patched id stays untouched.
            int bankCount;
            try
            {
                bankCount = TilesetTypeUtility.GetNumberOfAvailableTilesets();
            }
            catch (System.Exception)
            {
                bankCount = VanillaMaxExclusive;
            }

            if (index < bankCount)
            {
                return false;
            }

            DimensionTilesetRegistry.WarnUnknownIdOnce(index);
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        [HarmonyPatch(nameof(TilesetTypeUtility.GetTileset))]
        private static bool GetTileset(int index, ref PugMapTileset __result)
        {
            if (!ShouldHandle(index, out DimensionCustomTileset tileset))
            {
                return true;
            }

            PugMapTileset layers = tileset != null && tileset.LayersTemplate != null
                ? tileset.LayersTemplate
                : DimensionTilesetRegistry.ResolveFallbackLayers();
            if (layers == null)
            {
                // Bank not loaded yet — nothing can be rendering tiles; let vanilla decide.
                return true;
            }

            __result = layers;
            return false;
        }

        // A vanilla index a modder chose to reskin: serve the custom tileset's TEXTURES for it while
        // leaving everything else (layer rules, names, materials) vanilla. Render-only and save-safe.
        private static bool TryGetReskin(int index, out DimensionCustomTileset tileset)
        {
            tileset = null;
            return index >= 0 && index < VanillaMaxExclusive &&
                   DimensionTilesetRegistry.TryGetReskin(index, out tileset);
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        [HarmonyPatch(nameof(TilesetTypeUtility.GetTilesetTextures))]
        private static bool GetTilesetTextures(
            int tilesetIndex,
            ref MapWorkshopTilesetBank.TilesetTextures __result)
        {
            if (TryGetReskin(tilesetIndex, out DimensionCustomTileset reskin))
            {
                __result = reskin.Textures;
                return false;
            }

            if (!ShouldHandle(tilesetIndex, out DimensionCustomTileset tileset))
            {
                return true;
            }

            __result = tileset != null
                ? tileset.Textures
                : DimensionTilesetRegistry.GetMissingTextures();
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        [HarmonyPatch(nameof(TilesetTypeUtility.GetTexture))]
        private static bool GetTexture(
            int tilesetIndex,
            LayerName layerName,
            TextureType textureType,
            ref Texture2D __result)
        {
            if (TryGetReskin(tilesetIndex, out DimensionCustomTileset reskin))
            {
                __result = reskin.Textures != null ? reskin.Textures.GetTexture(textureType) : null;
                return false;
            }

            if (!ShouldHandle(tilesetIndex, out DimensionCustomTileset tileset))
            {
                return true;
            }

            if (tileset != null)
            {
                __result = tileset.Textures != null
                    ? tileset.Textures.GetTexture(textureType)
                    : null;
            }
            else
            {
                __result = textureType == TextureType.REGULAR
                    ? DimensionTilesetRegistry.GetMissingTexture()
                    : null;
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        [HarmonyPatch(nameof(TilesetTypeUtility.GetAdaptiveTexture))]
        private static bool GetAdaptiveTexture(
            int tilesetIndex,
            LayerName layerName,
            TextureType textureType,
            ref Texture2D __result)
        {
            if (TryGetReskin(tilesetIndex, out DimensionCustomTileset reskin))
            {
                __result = null;
                if (reskin.AdaptiveTextures.TryGetValue(
                        layerName, out MapWorkshopTilesetBank.TilesetTextures reskinAdaptive) &&
                    reskinAdaptive != null)
                {
                    __result = reskinAdaptive.GetTexture(textureType);
                }

                // No baked sheet for this layer → fall back to vanilla's own adaptive texture rather
                // than blanking the layer, so a partial reskin degrades gracefully.
                return __result == null ? true : false;
            }

            if (!ShouldHandle(tilesetIndex, out DimensionCustomTileset tileset))
            {
                return true;
            }

            __result = null;
            if (tileset != null &&
                tileset.AdaptiveTextures.TryGetValue(
                    layerName, out MapWorkshopTilesetBank.TilesetTextures adaptive) &&
                adaptive != null)
            {
                __result = adaptive.GetTexture(textureType);
            }

            // A null REGULAR texture is not "no texture" to the renderer — it means "fall back to the
            // layer template's own texture", and our custom tilesets borrow vanilla's shared template,
            // so the layer would paint itself with an unrelated tileset's art. Hand back a transparent
            // sheet instead so an unbaked layer draws nothing. Other texture types (emissive, normals,
            // effect mask) have no such fallback and are left null.
            if (__result == null && textureType == TextureType.REGULAR)
            {
                __result = DimensionTilesetRegistry.GetBlankTexture();
                DimensionTilesetRegistry.WarnMissingAdaptiveLayerOnce(tilesetIndex, layerName);
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        [HarmonyPatch(nameof(TilesetTypeUtility.GetOverrideMaterial))]
        private static bool GetOverrideMaterial(
            int tilesetIndex,
            LayerName layerName,
            ref Material __result)
        {
            if (!ShouldHandle(tilesetIndex, out DimensionCustomTileset tileset))
            {
                return true;
            }

            __result = null;
            if (tileset != null)
            {
                tileset.OverrideMaterials.TryGetValue(layerName, out __result);
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        [HarmonyPatch(nameof(TilesetTypeUtility.GetEditorOverrideMaterial))]
        private static bool GetEditorOverrideMaterial(
            int tilesetIndex,
            LayerName tileName,
            ref Material __result)
        {
            if (!ShouldHandle(tilesetIndex, out DimensionCustomTileset tileset))
            {
                return true;
            }

            __result = null;
            if (tileset != null)
            {
                tileset.OverrideMaterials.TryGetValue(tileName, out __result);
            }

            return false;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        [HarmonyPatch(nameof(TilesetTypeUtility.GetOverrideParticles))]
        private static bool GetOverrideParticles(
            int tilesetIndex,
            LayerName tileName,
            ref ParticleSystem __result)
        {
            if (!ShouldHandle(tilesetIndex, out DimensionCustomTileset _))
            {
                return true;
            }

            __result = null;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        [HarmonyPatch(nameof(TilesetTypeUtility.GetFriendlyName))]
        private static bool GetFriendlyName(int index, ref string __result)
        {
            if (!ShouldHandle(index, out DimensionCustomTileset tileset))
            {
                return true;
            }

            __result = tileset != null
                ? (string.IsNullOrEmpty(tileset.FriendlyName) ? tileset.Name : tileset.FriendlyName)
                : DimensionTilesetRegistry.GetMissingFriendlyName(index);
            return false;
        }
    }
}
