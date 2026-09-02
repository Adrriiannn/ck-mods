using System.Collections.Generic;
using ExpandNullforge.Generation;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using PugTilemap;
using PugTilemap.Quads;
using PugTilemap.Workshop;
using UnityEngine;

namespace ExpandNullforge.Tilesets
{
    /// <summary>
    /// Bridges authored <see cref="DimensionTilesetAsset"/>s (shipped in a consumer mod's
    /// bundle) into live <see cref="DimensionCustomTileset"/> registrations. The generated
    /// bootstrap calls <see cref="Register"/> from ModObjectLoaded — at mod load, before the
    /// ECS worlds exist — so rendering, placement and the map-color table all see the tileset
    /// before any tile of it can appear.
    /// </summary>
    public static class DimensionTilesetAssetRuntime
    {
        public static bool Register(DimensionTilesetAsset asset)
        {
            if (asset == null || !asset.Enabled || string.IsNullOrEmpty(asset.TilesetName))
            {
                return false;
            }

            if (asset.TilesetTexture == null)
            {
                DimensionLog.Problem(DimensionLogChannels.Tileset, null, 
                    "Custom tileset '" + asset.TilesetName +
                    "' has no tileset texture assigned; it will render as the missing placeholder.");
            }

            DimensionCustomTileset tileset = new DimensionCustomTileset(asset.TilesetName)
            {
                FriendlyName = asset.FriendlyName,
                Textures = new MapWorkshopTilesetBank.TilesetTextures
                {
                    texture = asset.TilesetTexture,
                    emissiveTexture = asset.EmissiveTexture
                }
            };

            // The ART is everything a reskin has, so it is built for both kinds.
            ApplyStateLayers(asset, tileset);
            ApplyGeneratedGen(asset, tileset);
            LogTilesetDetails(asset, tileset);

            // Reskin mode: this block's textures dress a VANILLA tileset index instead of placing its
            // own tiles. Register only the render override — no custom id, no items. Render-only and
            // save-safe: disabling the block simply stops the override next load.
            //
            // NOTHING BELOW THE ART IS REGISTERED FOR A RESKIN, AND THAT IS NOT AN OVERSIGHT. Map
            // colours, ground cover, ore veins and the two ground behaviours are all looked up by
            // the block's OWN tileset id, while the tiles a reskin dresses keep the game's id in the
            // world and in the save. Registering them would fill the tables with rows that could
            // never be matched — the shape of bug this framework calls an identity gate — and the
            // Tileset Studio hides those switches for a reskin for the same reason.
            if (asset.ItemMode == DimensionTilesetItemMode.ReskinVanilla)
            {
                if (asset.ReskinTilesetIndex < 0)
                {
                    DimensionLog.Problem(DimensionLogChannels.Tileset, null, 
                        "Reskin block '" + asset.TilesetName +
                        "' has no vanilla tileset chosen; nothing to reskin.");
                    return false;
                }

                return DimensionTilesetRegistry.RegisterReskin(asset.ReskinTilesetIndex, tileset);
            }

            // Ground-family types share the ground color so dug/watered ground stays coherent
            // on the world map; wall keeps its own.
            tileset.MapColors.Add(new DimensionTileMapColor(TileType.ground, asset.GroundMapColor));
            tileset.MapColors.Add(new DimensionTileMapColor(TileType.dugUpGround, asset.GroundMapColor));
            tileset.MapColors.Add(new DimensionTileMapColor(TileType.wateredGround, asset.GroundMapColor));
            tileset.MapColors.Add(new DimensionTileMapColor(TileType.wall, asset.WallMapColor));

            ApplyOverlayScatterRules(asset);
            ApplyOreVeinRules(asset);
            DimensionTilesetBehaviourRegistry.RegisterGroundBehaviour(asset.TilesetId, asset.SlimeBehaviour);
            DimensionTilesetBehaviourRegistry.RegisterSurfaceBehaviour(asset.TilesetId, asset.SurfaceBehaviour);

            // Material overrides are only served for our own custom ids (GetOverrideMaterial passes
            // vanilla indexes through), so a reskin block could never see the circuit material.
            ApplyCircuitFloor(asset, tileset);

            return DimensionTilesetRegistry.Register(tileset);
        }

        /// <summary>
        /// One block of everything worth knowing about a custom tileset at load, tagged [NF_TILESET].
        /// Written on every peer, so a host log and a client log can be diffed line for line — the
        /// numeric id is a hash of the identity string and MUST match across machines, since the id is
        /// what gets written into saved tiles and sent over the wire.
        /// </summary>
        private static void LogTilesetDetails(DimensionTilesetAsset asset, DimensionCustomTileset tileset)
        {
            Texture2D sheet = asset.TilesetTexture;

            DimensionLog.Trace(DimensionLogChannels.Tileset, null, 
                "identity='" + asset.TilesetName + "'  id=" + tileset.Id +
                "  mode=" + asset.ItemMode +
                (asset.ItemMode == DimensionTilesetItemMode.ReskinVanilla
                    ? "(vanilla " + asset.ReskinTilesetIndex + ")"
                    : string.Empty) +
                "  type=" + asset.BlockTypeKey);

            DimensionLog.Trace(DimensionLogChannels.Tileset, null, 
                "sheet=" + (sheet != null ? sheet.name + " " + sheet.width + "x" + sheet.height : "MISSING") +
                "  emissive=" + (asset.EmissiveTexture != null ? asset.EmissiveTexture.name : "none") +
                "  states=" + asset.Layers.Count +
                "  ores=" + asset.Ores.Count +
                "  oreScatter=" + asset.OreScatterCount +
                "  cover=" + asset.CoverCount);

            // Baked sheets are the whole rendering story; list them so a blank layer is traceable.
            string sheets = string.Empty;
            foreach (System.Collections.Generic.KeyValuePair<LayerName, MapWorkshopTilesetBank.TilesetTextures> pair
                     in tileset.AdaptiveTextures)
            {
                Texture2D bound = pair.Value != null ? pair.Value.GetTexture(TextureType.REGULAR) : null;
                sheets += (sheets.Length > 0 ? ", " : string.Empty) + pair.Key +
                          (bound != null ? "=" + bound.width + "x" + bound.height : "=NULL");
            }

            DimensionLog.Trace(DimensionLogChannels.Tileset, null, "adaptive sheets [" + tileset.AdaptiveTextures.Count + "]: " +
                      (sheets.Length > 0 ? sheets : "(none — every full-adaptive layer will render blank)"));

            // Ids live above the vanilla bank and below int.MaxValue (see MaxCustomTilesetIdExclusive).
            // Anything outside that band was generated against a different framework version — most
            // likely one that hashed into the old 16-bit band — and no longer names the same tileset.
            if (tileset.Id >= DimensionTilesetRegistry.MaxCustomTilesetIdExclusive ||
                tileset.Id < DimensionTilesetRegistry.MinCustomTilesetId)
            {
                DimensionLog.Problem(DimensionLogChannels.Tileset, null, 
                    "id " + tileset.Id + " is outside the safe range [" +
                    DimensionTilesetRegistry.MinCustomTilesetId + ", " +
                    DimensionTilesetRegistry.MaxCustomTilesetIdExclusive +
                    "). Regenerate the block against the current framework.");
            }
        }

        /// <summary>
        /// The under-glass circuit floor: routes the ground layer through the framework's circuit
        /// material (regular art + emissive circuit glow from rare ambient pulses and the game's
        /// live electricity texture). The tile renderer instantiates the override material and
        /// binds _MainTex (REGULAR) and _EmissiveTex (EMISSIVE) on it from this tileset's textures
        /// — for ground that's the baked GEN sheets via GetAdaptiveTexture — so the automatic
        /// binding normally wins. The explicit _EmissiveTex set below is belt-and-braces: it makes
        /// the material correct even when no GEN emissive exists yet (the renderer only overwrites
        /// _EmissiveTex when it resolved a non-null emissive).
        /// </summary>
        private static void ApplyCircuitFloor(DimensionTilesetAsset asset, DimensionCustomTileset tileset)
        {
            if (!asset.CircuitFloor)
            {
                return;
            }

            Material template = asset.CircuitFloorMaterial;
            if (template == null)
            {
                DimensionLog.Problem(DimensionLogChannels.Tileset, null, 
                    "Tileset '" + asset.TilesetName +
                    "' has the circuit floor enabled but no circuit-floor material reference; " +
                    "re-toggle it in the Tileset Studio so the material is assigned.");
                return;
            }

            // Instantiate so per-tileset texture binds never mutate the shared bundled asset.
            Material material = new Material(template);

            Texture2D emissive = null;
            foreach (DimensionGeneratedGenLayer g in asset.GeneratedGen)
            {
                if (g != null && g.layer == LayerName.ground && g.emissiveTexture != null)
                {
                    emissive = g.emissiveTexture;
                    break;
                }
            }

            if (emissive == null)
            {
                emissive = asset.EmissiveTexture;
            }

            if (emissive != null)
            {
                material.SetTexture("_EmissiveTex", emissive);
            }

            tileset.OverrideMaterials[LayerName.ground] = material;
        }

        /// <summary>
        /// The native-rendering hookup. Each generated GEN sheet is baked in Core Keeper's exact canonical
        /// layout, so the game's (dirt-fallback) full-adaptive lookup table indexes it correctly — filling
        /// <c>AdaptiveTextures[layer]</c> is the entire allocation. This is the single spot that was the
        /// gap (<c>GetAdaptiveTexture</c> returned null for ground/wall → the engine fell back to dirt).
        /// </summary>
        private static void ApplyGeneratedGen(DimensionTilesetAsset asset, DimensionCustomTileset tileset)
        {
            if (asset.GeneratedGen == null || asset.GeneratedGen.Count == 0)
            {
                DimensionLog.Problem(DimensionLogChannels.Tileset, null, 
                    "Tileset '" + asset.TilesetName + "' has no baked sheets at all. " +
                    "Its ground and wall will render blank — open the Tileset Studio and generate it.");
                return;
            }

            string bound = string.Empty;
            string missing = string.Empty;
            foreach (DimensionGeneratedGenLayer g in asset.GeneratedGen)
            {
                if (g == null || !System.Enum.IsDefined(typeof(LayerName), g.layer))
                {
                    continue;
                }

                // A baked entry whose texture failed to load is the dangerous case: skipped in
                // silence it leaves the layer inheriting an unrelated tileset's art.
                if (g.texture == null)
                {
                    missing += (missing.Length > 0 ? ", " : string.Empty) + g.layer;
                    continue;
                }

                tileset.AdaptiveTextures[g.layer] = new MapWorkshopTilesetBank.TilesetTextures
                {
                    texture = g.texture,
                    emissiveTexture = g.emissiveTexture,
                };
                bound += (bound.Length > 0 ? ", " : string.Empty) + g.layer;
            }

            DimensionFrameworkLog.Verbose(
                "Tileset '" + asset.TilesetName + "' bound adaptive sheets: " +
                (bound.Length > 0 ? bound : "(none)") + ".");

            if (missing.Length > 0)
            {
                DimensionLog.Problem(DimensionLogChannels.Tileset, null, 
                    "Tileset '" + asset.TilesetName +
                    "' is missing the baked texture for: " + missing +
                    ". Those layers will render blank; regenerate the tileset and rebuild the mod.");
            }
        }

        /// <summary>
        /// Feeds any state the modder switched on AND gave its own texture into the tileset's
        /// per-layer override map, so that one layer renders from the custom texture instead of the
        /// main sheet. A state that is on but has no custom texture needs nothing here — it already
        /// renders from the main dirt-layout sheet through the vanilla layer rules.
        /// </summary>
        private static void ApplyStateLayers(DimensionTilesetAsset asset, DimensionCustomTileset tileset)
        {
            foreach (DimensionTilesetLayerConfig config in asset.Layers)
            {
                if (config == null || config.texture == null)
                {
                    continue;
                }

                DimensionTilesetState state;
                if (!DimensionTilesetStateCatalog.TryGet(config.key, out state))
                {
                    continue;
                }

                // Required states (the surface) are always on even if their stored entry reads
                // disabled; an optional state must have been switched on.
                if (!config.enabled && !state.Required)
                {
                    continue;
                }

                tileset.AdaptiveTextures[state.Layer] = new MapWorkshopTilesetBank.TilesetTextures
                {
                    texture = config.texture
                };
            }
        }

        /// <summary>
        /// Tells the generator what this block scatters over its own ground.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Ticking "grass tufts" renders a block's grass art; without this registration nothing ever
        /// places any.
        /// Core Keeper scatters vanilla overlays from generation code that names tilesets by hardcoded
        /// id, so a custom tileset is never a candidate however complete its art is — the block looks
        /// finished in the Studio and comes out bare in the world.
        /// </para>
        /// <para>
        /// Registered even when the block has no art for the overlay yet: the density is the author's
        /// intent, and a missing texture is a separate problem that the sheet generation reports.
        /// </para>
        /// </remarks>
        private static void ApplyOverlayScatterRules(DimensionTilesetAsset asset)
        {
            List<DimensionOverlayRule> rules = new List<DimensionOverlayRule>();

            // Reads the ARRAYS, never asset.Layers: a serialized List of a custom class arrives
            // empty at mod runtime, so the old read compiled, ran, found nothing and registered
            // nothing — four toggles and four sliders that placed grass in the editor's head only.
            // EditorSetGroundCover fills the arrays at Generate for exactly this read.
            for (int i = 0; i < asset.CoverCount; i++)
            {
                string stateKey;
                float density;
                if (!asset.TryGetCover(i, out stateKey, out density) || density <= 0f)
                {
                    continue;
                }

                LayerName layer;
                TileType tileType;
                if (!DimensionOverlayRuleRegistry.TryGetScatterLayer(stateKey, out layer, out tileType))
                {
                    continue;
                }

                rules.Add(new DimensionOverlayRule(layer, tileType, density));
            }

            DimensionOverlayRuleRegistry.Register(asset.TilesetId, rules);
        }

        /// <summary>
        /// Registers the block's vein rules for natural scattering in generated walls.
        /// </summary>
        /// <remarks>
        /// Reads the ARRAYS, never <c>asset.Ores</c>: a serialized List of a custom class
        /// arrives empty at mod runtime, and the arrays are the mirror the generator fills at
        /// Generate precisely so this read survives. The <c>oreScatter=</c> line in the
        /// [NF_TILESET] log is the in-game proof.
        /// </remarks>
        private static void ApplyOreVeinRules(DimensionTilesetAsset asset)
        {
            List<Generation.DimensionOreVeinRule> rules =
                new List<Generation.DimensionOreVeinRule>();
            for (int i = 0; i < asset.OreScatterCount; i++)
            {
                string itemId;
                bool isCustom;
                float abundance;
                int sizeMin;
                int sizeMax;
                if (!asset.TryGetOreScatter(i, out itemId, out isCustom, out abundance, out sizeMin, out sizeMax) ||
                    abundance <= 0f)
                {
                    continue;
                }

                // The carrier is always the block's OWN tileset: the generated vein object
                // stamped (own tileset, ore), and the drop resolves by first match on the pair.
                rules.Add(new Generation.DimensionOreVeinRule(
                    asset.TilesetId, itemId, abundance, sizeMin, sizeMax));
            }

            Generation.DimensionOreVeinRuleRegistry.Register(asset.TilesetId, rules);
        }
    }
}
