using System.Collections.Generic;
using System.Text;
using ExpandNullforge.Tilesets;
using PugTilemap;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // NOTE: ScriptableObject asset classes MUST live in a file named after the class — Unity
    // only binds .asset files to the MonoScript matching the filename (see the 2026-07-23
    // filename-binding incident).
    /// <summary>
    /// One custom tileset authored in the dashboard. Ships inside the consumer mod's bundle;
    /// the generated bootstrap registers it at mod load (before the ECS worlds exist, so
    /// rendering, placement and map colors are all wired before any tile can be seen).
    ///
    /// The modder types one friendly <see cref="BlockName"/> ("Eerie Stone"); the backend derives
    /// everything else. The stable identity <see cref="TilesetName"/> is "{mod}:{name}" — the
    /// numeric tileset id is a hash of it, so world saves stay valid across any mod-set or
    /// load-order change (renaming changes the identity and orphans existing tiles). The texture
    /// must follow the vanilla dirt sheet layout; rendering reuses the dirt layer rules
    /// positionally, so only the texture differs. Per-block display names come from the block name;
    /// icons/descriptions live on the generated block items, not here.
    /// </summary>
    [CreateAssetMenu(menuName = "Dimensions API/Tileset")]
    public sealed class DimensionTilesetAsset : ScriptableObject
    {
        [Tooltip("The block's name, e.g. \"Eerie Stone\". The ground block takes this name and the wall block adds \" Wall\". The mod id is added automatically behind the scenes.")]
        [SerializeField] private string blockName = "New Block";
        // Mod id stamped once at creation (e.g. "Nullforge"); the identity is "{modPrefix}:{name}".
        // Hidden from the modder — it is not something they should have to think about.
        [HideInInspector]
        [SerializeField] private string modPrefix = "Mod";
        [Tooltip("The tileset sheet. MUST follow the vanilla dirt_tileset.png layout (wall top + wall cross, ground patch + ground cross, wall front band, shadow cross; decorations optional).")]
        [SerializeField] private Texture2D tilesetTexture;
        [Tooltip("Tick only if this tileset glows in the dark (lava, crystal, eerie-glow). Most blocks leave this off.")]
        [SerializeField] private bool isEmissive;
        [Tooltip("The glow layer (same sheet layout): which pixels emit light in the dark. Used only when 'Is emissive?' is on.")]
        [SerializeField] private Texture2D emissiveTexture;
        [SerializeField] private bool enabled = true;
        [Tooltip("Under-glass circuit glow: the ground renders its regular art (dark glass + dormant traces) and the circuit emissive art lights up — rare deterministic ambient pulses, plus real powered electricity nearby.")]
        [SerializeField] private bool circuitFloor;

        [Tooltip("Sit perfectly straight instead of taking Core Keeper's hand-drawn wobble. Right for " +
                 "machined surfaces — metal plating, glass panels — where a crooked edge reads as a bug.")]
        [SerializeField] private bool rigidSurface;
        // The framework-shipped circuit-floor material (Materials/NullforgeCircuitFloor.mat).
        // Auto-assigned by the Studio when the toggle turns on; the serialized reference is what
        // pulls the material + shader into the mod bundle. Infrastructure, so hidden.
        [HideInInspector]
        [SerializeField] private Material circuitFloorMaterial;

        [Header("World map colors")]
        [SerializeField] private Color32 groundMapColor = new Color32(122, 86, 57, 255);
        [SerializeField] private Color32 wallMapColor = new Color32(70, 48, 32, 255);

        // ---- Modular block model (set once by the Add-block wizard) ----
        // Which primary type this block is (key into DimensionTilesetTypeCatalog). Legacy assets
        // created before the wizard have an empty key and resolve to "terrain".
        [HideInInspector]
        [SerializeField] private string blockType = "terrain";
        // How the tileset reaches the game: its own item, none, or by reskinning a vanilla tileset.
        [HideInInspector]
        [SerializeField] private int itemMode = (int)DimensionTilesetItemMode.CreateItem;
        // When ItemMode == ReskinVanilla: the vanilla Tileset index (0-74) this block reskins.
        [HideInInspector]
        [SerializeField] private int reskinTilesetIndex = -1;

        // Per-state authoring, keyed by DimensionTilesetStateCatalog entries. Written by the Tileset
        // Studio; a state absent from the list is off and renders from the main sheet.
        [SerializeField] private List<DimensionTilesetLayerConfig> layers = new List<DimensionTilesetLayerConfig>();

        // The baked full-adaptive ("GEN") sheets, one per layer, produced at edit time by the tileset
        // generator and stored here so the mint files ship in the bundle. Empty until the modder runs
        // Generate; when empty the tileset falls back to dirt's adaptive tables (the pre-generator path).
        // Stored as three PARALLEL PRIMITIVE ARRAYS rather than a List of a custom class, because a
        // List<customClass> does not survive into the mod runtime: the game recompiles a mod's scripts
        // at load, and the bundle's nested-class data comes back empty even though the asset and its
        // textures ship correctly. Simple arrays of ints and UnityEngine.Object references do survive.
        // (The framework already works around this elsewhere by storing its tile map as a JSON string.)
        // Symptom when this regresses: every full-adaptive layer renders blank or borrows a foreign
        // tileset's art, while the main sheet — a plain object reference — looks fine.
        [HideInInspector]
        [SerializeField] private int[] generatedGenLayers = new int[0];

        [HideInInspector]
        [SerializeField] private Texture2D[] generatedGenTextures = new Texture2D[0];

        [HideInInspector]
        [SerializeField] private Texture2D[] generatedGenEmissiveTextures = new Texture2D[0];

        // Ores this block's walls can hold as veins (control-panel authored; generator emits the
        // hidden vein objects that give each (ore, tileset) tile its identity and drop).
        [HideInInspector]
        [SerializeField] private List<DimensionTilesetOreConfig> ores = new List<DimensionTilesetOreConfig>();

        /// <summary>The friendly block name the modder typed ("Eerie Stone").</summary>
        public string BlockName
        {
            get { return string.IsNullOrEmpty(blockName) ? "Block" : blockName; }
        }

        /// <summary>Alias of <see cref="BlockName"/> for diagnostics/registry display.</summary>
        public string FriendlyName
        {
            get { return BlockName; }
        }

        public Texture2D TilesetTexture
        {
            get { return tilesetTexture; }
        }

        /// <summary>Whether this tileset self-illuminates. When off, no emissive is applied.</summary>
        public bool IsEmissive
        {
            get { return isEmissive; }
        }

        /// <summary>The glow sheet, or null when the tileset is not marked emissive.</summary>
        public Texture2D EmissiveTexture
        {
            get { return isEmissive ? emissiveTexture : null; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        /// <summary>
        /// Under-glass circuit glow: the ground layer renders through the framework's circuit-floor
        /// material — regular art as-is, plus the circuit emissive art driven by rare deterministic
        /// ambient pulses and by the game's live electricity texture.
        /// </summary>
        public bool CircuitFloor
        {
            get { return circuitFloor; }
        }

        /// <summary>
        /// Whether this block's tiles opt out of Core Keeper's per-vertex noise displacement, so their
        /// art renders exactly as drawn. Walls are already exempt in vanilla; this extends that to the
        /// rest of the block.
        /// </summary>
        public bool RigidSurface
        {
            get { return rigidSurface; }
        }

        /// <summary>The framework circuit-floor material (Studio-assigned), or null when unset.</summary>
        public Material CircuitFloorMaterial
        {
            get { return circuitFloorMaterial; }
        }

        public Color32 GroundMapColor
        {
            get { return groundMapColor; }
        }

        public Color32 WallMapColor
        {
            get { return wallMapColor; }
        }

        /// <summary>This block's primary type (legacy assets with no stored key resolve to terrain).</summary>
        public DimensionTilesetType BlockType
        {
            get { return DimensionTilesetTypeCatalog.Resolve(blockType); }
        }

        /// <summary>The stored type key (may be empty on legacy assets).</summary>
        public string BlockTypeKey
        {
            get { return string.IsNullOrEmpty(blockType) ? "terrain" : blockType; }
        }

        /// <summary>How this block reaches the game: own item, none, or a vanilla reskin.</summary>
        public DimensionTilesetItemMode ItemMode
        {
            get { return (DimensionTilesetItemMode)itemMode; }
        }

        /// <summary>The vanilla Tileset index this block reskins (only when ItemMode is ReskinVanilla).</summary>
        public int ReskinTilesetIndex
        {
            get { return reskinTilesetIndex; }
        }

        /// <summary>
        /// Whether this tileset produces its single placeable "{name} Block" item — derived purely
        /// from the wizard's item-mode decision (no separate toggle to drift out of sync): the type
        /// supports an item and the block is in create-item mode (not a reskin).
        /// </summary>
        public bool GenerateBlock
        {
            get
            {
                return BlockType.SupportsItem &&
                       ItemMode == DimensionTilesetItemMode.CreateItem;
            }
        }

        // The framework generates TWO objects under one item, exactly like vanilla Dirt: the
        // player-facing wall "block" (obtainable, customizable) and a hidden, recipe-less ground
        // counterpart that makes ground-on-empty / wall-on-ground placement and the shared drop work.
        // Both are gated by the single GenerateBlock toggle, so the two kinds never diverge.
        public bool GenerateGroundBlock
        {
            get { return GenerateBlock; }
        }

        public bool GenerateWallBlock
        {
            get { return GenerateBlock; }
        }

        /// <summary>All authored per-state layer configs (read-only view for registration/preview).</summary>
        public IReadOnlyList<DimensionTilesetLayerConfig> Layers
        {
            get { return layers ?? (layers = new List<DimensionTilesetLayerConfig>()); }
        }

        /// <summary>
        /// The baked GEN sheets (one per layer) the generator stored on this asset, or an empty list
        /// before Generate has ever run. <see cref="DimensionTilesetAssetRuntime"/> feeds these into
        /// the live tileset's adaptive textures so it renders natively.
        /// </summary>
        public IReadOnlyList<DimensionGeneratedGenLayer> GeneratedGen
        {
            get
            {
                List<DimensionGeneratedGenLayer> baked = new List<DimensionGeneratedGenLayer>();
                if (generatedGenLayers == null || generatedGenTextures == null)
                {
                    return baked;
                }

                int count = Mathf.Min(generatedGenLayers.Length, generatedGenTextures.Length);
                for (int i = 0; i < count; i++)
                {
                    baked.Add(new DimensionGeneratedGenLayer
                    {
                        layer = (PugTilemap.LayerName)generatedGenLayers[i],
                        texture = generatedGenTextures[i],
                        emissiveTexture =
                            generatedGenEmissiveTextures != null && i < generatedGenEmissiveTextures.Length
                                ? generatedGenEmissiveTextures[i]
                                : null
                    });
                }

                return baked;
            }
        }

        /// <summary>The ores this block's walls can hold (empty = none).</summary>
        public IReadOnlyList<DimensionTilesetOreConfig> Ores
        {
            get { return ores ?? (ores = new List<DimensionTilesetOreConfig>()); }
        }

        /// <summary>
        /// Whether this tileset has baked GEN sheets and therefore renders from its own adaptive data
        /// rather than borrowing dirt's tables.
        /// </summary>
        public bool HasGeneratedGen
        {
            get { return generatedGenLayers != null && generatedGenLayers.Length > 0; }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only: replace the stored baked GEN sheets wholesale. Called by the tileset generator
        /// after it bakes ground/wall (and later fronts/states). Runtime code never mutates this.
        /// </summary>
        public void EditorSetGeneratedGen(List<DimensionGeneratedGenLayer> baked)
        {
            int count = baked == null ? 0 : baked.Count;
            generatedGenLayers = new int[count];
            generatedGenTextures = new Texture2D[count];
            generatedGenEmissiveTextures = new Texture2D[count];
            for (int i = 0; i < count; i++)
            {
                DimensionGeneratedGenLayer entry = baked[i];
                generatedGenLayers[i] = entry == null ? 0 : (int)entry.layer;
                generatedGenTextures[i] = entry == null ? null : entry.texture;
                generatedGenEmissiveTextures[i] = entry == null ? null : entry.emissiveTexture;
            }
        }
#endif

        /// <summary>The config for one state key, or null if the modder never touched it.</summary>
        public DimensionTilesetLayerConfig FindLayer(string key)
        {
            if (layers == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i] != null && string.Equals(layers[i].key, key, System.StringComparison.Ordinal))
                {
                    return layers[i];
                }
            }

            return null;
        }

        // The farm trio is the only modder choice left; every other state is texture availability
        // for things the game does anyway (cracks, slime, pebbles, worldgen overlays) and is always on.
        private static bool IsFarmStateKey(string key)
        {
            return key == "tilled" || key == "watered" || key == "flooded";
        }

        /// <summary>
        /// Whether a state is switched on. Required states and every non-farm overlay are always on
        /// (verified: their in-game drivers are player/world systems the block can't opt out of);
        /// only the farm trio follows the modder's "can it be farmed?" choice.
        /// </summary>
        public bool IsStateEnabled(string key)
        {
            DimensionTilesetState state;
            if (!DimensionTilesetStateCatalog.TryGet(key, out state))
            {
                return false;
            }

            if (state.Required || !IsFarmStateKey(key))
            {
                return true;
            }

            DimensionTilesetLayerConfig config = FindLayer(key);
            return config != null && config.enabled;
        }

        /// <summary>The config for a state, creating (and storing) an off entry if it did not exist.</summary>
        public DimensionTilesetLayerConfig GetOrCreateLayer(string key)
        {
            if (layers == null)
            {
                layers = new List<DimensionTilesetLayerConfig>();
            }

            DimensionTilesetLayerConfig existing = FindLayer(key);
            if (existing != null)
            {
                return existing;
            }

            DimensionTilesetLayerConfig created = new DimensionTilesetLayerConfig { key = key };
            layers.Add(created);
            return created;
        }

        /// <summary>
        /// The stable identity string "{mod}:{name}", derived from the friendly block name. This is
        /// what the numeric tileset id hashes from, so it must stay stable once tiles are in a save.
        /// </summary>
        public string TilesetName
        {
            get
            {
                string prefix = string.IsNullOrEmpty(modPrefix) ? "Mod" : modPrefix;
                return prefix + ":" + NormalizeToken(BlockName);
            }
        }

        /// <summary>The derived numeric tileset id (stable hash of the identity name).</summary>
        public int TilesetId
        {
            get { return Tilesets.DimensionTilesetRegistry.ComputeTilesetId(TilesetName); }
        }

        /// <summary>Item id of the generated ground block ("&lt;identity&gt;.ground.block").</summary>
        public string GroundBlockItemId
        {
            get { return TilesetName + ".ground.block"; }
        }

        /// <summary>Item id of the generated wall block ("&lt;identity&gt;.wall.block").</summary>
        public string WallBlockItemId
        {
            get { return TilesetName + ".wall.block"; }
        }

        /// <summary>
        /// The display name for a block object. The player-facing wall object is the single
        /// "{name} Block" item the modder sees and edits; the ground object is hidden infrastructure
        /// named "{name} Ground" so it reads sensibly if ever surfaced, mirroring vanilla's
        /// obtainable "Dirt Wall" + non-obtainable "Dirt Ground" pair.
        /// </summary>
        public string ResolveBlockDisplayName(TileType tileType)
        {
            return tileType == TileType.wall ? BlockName + " Block" : BlockName + " Ground";
        }

        /// <summary>
        /// Strips a friendly name down to an id token: letters and digits only. "Eerie Stone" ->
        /// "EerieStone". Keeps the identity clean and stable for item ids and localization keys.
        /// </summary>
        private static string NormalizeToken(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return "Block";
            }

            StringBuilder builder = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (char.IsLetterOrDigit(c))
                {
                    builder.Append(c);
                }
            }

            return builder.Length == 0 ? "Block" : builder.ToString();
        }
    }
}
