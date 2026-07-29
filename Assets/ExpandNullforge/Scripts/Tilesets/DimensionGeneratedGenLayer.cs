using System;
using PugTilemap;
using UnityEngine;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// One baked full-adaptive ("GEN") sheet for a single tilemap layer, generated at edit time and
    /// stored on the <see cref="Authoring.DimensionTilesetAsset"/> so the "mint" files ship in the mod
    /// bundle rather than being re-baked at runtime.
    ///
    /// At mod load <see cref="DimensionTilesetAssetRuntime"/> hands each of these to the tileset's
    /// <c>AdaptiveTextures[layer]</c>. Because the sheet is packed in Core Keeper's exact canonical
    /// adaptive-texture layout, the game's own full-adaptive lookup table indexes it correctly and the
    /// tileset renders natively — no per-tileset lookup table needs to be built or injected.
    /// </summary>
    [Serializable]
    public sealed class DimensionGeneratedGenLayer
    {
        [Tooltip("Which tilemap layer this baked sheet feeds (ground, wall, ...).")]
        public LayerName layer;

        [Tooltip("The baked GEN sheet, packed in Core Keeper's canonical adaptive-texture layout.")]
        public Texture2D texture;

        [Tooltip("The baked emissive GEN sheet (same layout; pixels = emitted light), or null when the tileset does not glow.")]
        public Texture2D emissiveTexture;
    }
}
