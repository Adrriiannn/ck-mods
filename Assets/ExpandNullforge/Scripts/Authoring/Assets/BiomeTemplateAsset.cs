using ExpandNullforge.Api;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    [CreateAssetMenu(menuName = "Dimensions API/Biome Template")]
    public sealed class BiomeTemplateAsset : ScriptableObject
    {
        // The environment-profile and palette template layers, the content-preset mixin, and the
        // per-biome resource-node / spawn-rule / generation-table lists that once sat here were
        // Phase-0 surfaces nothing consumed. The separate Biome Generation Profile asset that used
        // to hold a second list of generation passes was folded into generationPasses below, so the
        // steps a biome runs are all in one place. A biome IS its block lists now: music and ambience
        // live directly below, ground fog and map colour ride the tileset, creatures ride the
        // per-creature spawn chain, and ore patches ride the tileset's ore config.
        [SerializeField] private string biomeId = "biome";
        [SerializeField] private string displayName = "Biome";
        [SerializeField] private string environmentProfileId = string.Empty;
        [SerializeField] private string paletteAssetId = string.Empty;
        [SerializeField] private string spawnTableId = string.Empty;
        [SerializeField] private string resourceTableId = string.Empty;
        [SerializeField] private string worldEventTableId = string.Empty;
        [SerializeField] private Color mapColor = new Color(0.25f, 0.45f, 0.55f, 1f);
        [SerializeField] private int priority;
        [SerializeField] private bool enabled = true;
        [SerializeField] private bool hasFallbackLocalBounds;
        [SerializeField] private Vector2Int fallbackLocalMin = new Vector2Int(-64, -64);
        [SerializeField] private Vector2Int fallbackLocalMaxExclusive = new Vector2Int(64, 64);
        [SerializeField] private string[] floorObjectIds = new string[0];
        [SerializeField] private string[] wallObjectIds = new string[0];
        [SerializeField] private string[] oreObjectIds = new string[0];
        [SerializeField] private SceneTemplateAsset[] scenePool = new SceneTemplateAsset[0];
        [SerializeField] private GenerationPassTemplateAsset[] generationPasses = new GenerationPassTemplateAsset[0];
        [Tooltip("Announce this biome with a title card the first time a player walks into it.")]
        [SerializeField] private bool showTitleOnDiscovery = true;

        [Tooltip("Colour of the title text and the gamepad light. Blank uses the map colour.")]
        [SerializeField] private bool overrideTitleColor;
        [SerializeField] private Color titleColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        [Tooltip("Object whose icon flanks the title. Leave empty for no icons.")]
        [SerializeField] private string titleIconObjectId = string.Empty;

        [Tooltip("Looping ambience for this biome, picked from the Sound Library. Empty for none.")]
        [SerializeField] private string ambienceSoundKey = string.Empty;

        [Tooltip("How loud this biome's ambience gets at its fullest.")]
        [Range(0f, 2f)]
        [SerializeField] private float ambienceVolume = 1f;

        [Tooltip("Which of the game's music playlists this biome plays. Empty keeps the surrounding music.")]
        [SerializeField] private string musicRosterName = string.Empty;

        [SerializeField] private string notes = string.Empty;

        private void OnValidate()
        {
            scenePool = CopyObjects(scenePool);
            generationPasses = CopyObjects(generationPasses);
        }

        public string BiomeId
        {
            get { return biomeId ?? string.Empty; }
        }

        public string DisplayName
        {
            get { return displayName ?? string.Empty; }
        }

        public string Notes
        {
            get { return notes ?? string.Empty; }
        }

        /// <summary>Whether walking into this biome announces it with a title card.</summary>
        /// <remarks>
        /// On by default, because a biome nobody is told they have entered reads as an art change
        /// rather than a place. An author who wants a seamless transition — a cave that is really part
        /// of the biome above it — turns it off.
        /// </remarks>
        public bool ShowTitleOnDiscovery
        {
            get { return showTitleOnDiscovery; }
        }

        /// <summary>
        /// The colour of the title, falling back to the map colour.
        /// </summary>
        /// <remarks>
        /// Falls back rather than defaulting to white so a biome gets a sensible title colour for free:
        /// the map colour is already chosen to say "this place", and the two matching is what makes the
        /// title feel like part of the biome rather than a caption over it.
        /// </remarks>
        public Color TitleColor
        {
            get { return overrideTitleColor ? titleColor : mapColor; }
        }

        /// <summary>The object whose icon flanks the title, or empty for none.</summary>
        public string TitleIconObjectId
        {
            get { return titleIconObjectId ?? string.Empty; }
        }

        /// <summary>The looping ambience this biome adds to the game's own mix, or empty for none.</summary>
        public string AmbienceSoundKey
        {
            get { return ambienceSoundKey ?? string.Empty; }
        }

        /// <summary>
        /// How loud the ambience gets at its fullest.
        /// </summary>
        /// <remarks>
        /// A multiplier on the volume Core Keeper works out from how much of the biome surrounds the
        /// player, not a fixed level — so the sound still swells as they walk in rather than snapping
        /// on at the boundary.
        /// </remarks>
        public float AmbienceVolume
        {
            get { return ambienceVolume < 0f ? 0f : ambienceVolume; }
        }

        /// <summary>
        /// The name of the game's music playlist this biome plays, or empty to keep the surrounding
        /// music.
        /// </summary>
        /// <remarks>
        /// Empty is a reasonable default rather than a gap: a small biome inside a larger one usually
        /// wants the host's music to keep playing, and interrupting it every time the player steps
        /// through a doorway is worse than no music of its own.
        /// </remarks>
        public string MusicRosterName
        {
            get { return musicRosterName ?? string.Empty; }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        public int Priority
        {
            get { return priority; }
        }

        public bool HasFallbackLocalBounds
        {
            get { return hasFallbackLocalBounds; }
        }

        public DimensionBounds FallbackLocalBounds
        {
            get
            {
                return new DimensionBounds(
                    new int2(fallbackLocalMin.x, fallbackLocalMin.y),
                    new int2(fallbackLocalMaxExclusive.x, fallbackLocalMaxExclusive.y));
            }
        }

        public SceneTemplateAsset[] ScenePool
        {
            get { return scenePool ?? new SceneTemplateAsset[0]; }
        }

        public string EnvironmentProfileId
        {
            get { return environmentProfileId ?? string.Empty; }
        }

        public string PaletteAssetId
        {
            get { return paletteAssetId ?? string.Empty; }
        }

        /// <summary>The generation steps this biome runs, in order.</summary>
        /// <remarks>
        /// <para>
        /// The only list of them. A project authored before the fold splits a biome's passes across
        /// this list and a separate Biome Generation Profile asset prepended to it, only one of
        /// which is editable where the biome is edited; that asset had no creation path anywhere in
        /// the product, so the only ones that exist are the ones the starter factory made, and
        /// <c>DimensionBiomeGenerationProfileFold</c> brings their passes into this list.
        /// </para>
        /// <para>
        /// Nothing was lost by folding: a pass is itself a standalone shared asset, so several
        /// biomes wanting the same recipe reference the same pass assets from their own lists.
        /// </para>
        /// <para>
        /// Returns a copy with the empty slots stripped, because a list a creator has grown in the
        /// inspector routinely holds a trailing null and callers here index straight into it.
        /// </para>
        /// </remarks>
        public GenerationPassTemplateAsset[] GenerationPasses
        {
            get { return CopyObjects(generationPasses); }
        }

        /// <summary>The blocks this biome's floors are made of. A biome IS its block lists.</summary>
        public string[] FloorObjectIds
        {
            get { return CopyNonEmptyStrings(floorObjectIds); }
        }

        public string[] WallObjectIds
        {
            get { return CopyNonEmptyStrings(wallObjectIds); }
        }

        public string[] OreObjectIds
        {
            get { return CopyNonEmptyStrings(oreObjectIds); }
        }

        public DimensionBiomeDefinition ToBiomeDefinition(string dimensionId)
        {
            return new DimensionBiomeDefinition(
                biomeId,
                displayName,
                dimensionId,
                environmentProfileId,
                paletteAssetId,
                spawnTableId,
                resourceTableId,
                worldEventTableId,
                ToRgba(mapColor),
                priority,
                enabled,
                notes);
        }

        public void ConfigureIdentity(
            string newBiomeId,
            string newDisplayName,
            Color newMapColor,
            int newPriority,
            bool newEnabled)
        {
            biomeId = newBiomeId ?? string.Empty;
            displayName = string.IsNullOrEmpty(newDisplayName)
                ? biomeId
                : newDisplayName;
            mapColor = newMapColor;
            priority = newPriority;
            enabled = newEnabled;
        }

        public void ApplyCustomizerMetadata(
            string newBiomeId,
            string newDisplayName,
            int newPriority,
            bool newEnabled,
            string newEnvironmentProfileId,
            string newPaletteAssetId,
            string newNotes)
        {
            biomeId = newBiomeId ?? string.Empty;
            displayName = string.IsNullOrEmpty(newDisplayName)
                ? biomeId
                : newDisplayName;
            priority = newPriority;
            enabled = newEnabled;
            environmentProfileId = newEnvironmentProfileId ?? string.Empty;
            paletteAssetId = newPaletteAssetId ?? string.Empty;
            notes = newNotes ?? string.Empty;
        }

        public void SetScenePool(IReadOnlyList<SceneTemplateAsset> scenes)
        {
            scenePool = CopyObjects(scenes);
        }

        public void SetGenerationPasses(IReadOnlyList<GenerationPassTemplateAsset> passes)
        {
            generationPasses = CopyObjects(passes);
        }

        public void ApplyFallbackLocalBounds(
            Vector2Int localMin,
            Vector2Int localMaxExclusive)
        {
            hasFallbackLocalBounds = true;
            fallbackLocalMin = localMin;
            fallbackLocalMaxExclusive = EnsureExclusiveMax(localMin, localMaxExclusive);
        }

        public void ClearFallbackLocalBounds()
        {
            hasFallbackLocalBounds = false;
        }

        public void ApplySemanticTerrainPreset(
            string[] floorIds,
            string[] wallIds,
            string[] oreIds,
            bool replaceExisting)
        {
            floorObjectIds = replaceExisting
                ? CopyNonEmptyStrings(floorIds)
                : MergeStringArrays(floorObjectIds, floorIds);
            wallObjectIds = replaceExisting
                ? CopyNonEmptyStrings(wallIds)
                : MergeStringArrays(wallObjectIds, wallIds);
            oreObjectIds = replaceExisting
                ? CopyNonEmptyStrings(oreIds)
                : MergeStringArrays(oreObjectIds, oreIds);
            notes = "Semantic terrain preset applied.";
        }

        public void ApplyMinimalBiomePreset(
            string newBiomeId,
            string newDisplayName,
            Color newMapColor,
            Vector2Int localMin,
            Vector2Int localMaxExclusive,
            string[] floorIds,
            string[] wallIds)
        {
            ConfigureIdentity(newBiomeId, newDisplayName, newMapColor, priority, true);
            ApplyFallbackLocalBounds(localMin, localMaxExclusive);
            ApplySemanticTerrainPreset(
                floorIds,
                wallIds,
                new string[0],
                true);
            notes = "Generated from the minimal biome preset.";
        }

        private static uint ToRgba(Color color)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255);
            uint a = (uint)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255);
            return (r << 24) | (g << 16) | (b << 8) | a;
        }

        private static Vector2Int EnsureExclusiveMax(
            Vector2Int localMin,
            Vector2Int localMaxExclusive)
        {
            int maxX = localMaxExclusive.x <= localMin.x
                ? localMin.x + 1
                : localMaxExclusive.x;
            int maxY = localMaxExclusive.y <= localMin.y
                ? localMin.y + 1
                : localMaxExclusive.y;
            return new Vector2Int(maxX, maxY);
        }

        private static string[] CopyNonEmptyStrings(string[] source)
        {
            List<string> values = new List<string>();
            AddStringRange(source, values);
            return values.ToArray();
        }

        private static string[] MergeStringArrays(string[] existing, string[] incoming)
        {
            List<string> values = new List<string>();
            AddStringRange(existing, values);
            AddUniqueStringRange(incoming, values);
            return values.ToArray();
        }

        private static T[] CopyObjects<T>(IReadOnlyList<T> source)
            where T : Object
        {
            if (source == null || source.Count == 0)
            {
                return new T[0];
            }

            List<T> destination = new List<T>();
            for (int i = 0; i < source.Count; i++)
            {
                T item = source[i];
                if (item != null)
                {
                    destination.Add(item);
                }
            }

            return destination.ToArray();
        }

        private static void AddUniqueStringRange(string[] source, List<string> destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                string item = source[i] ?? string.Empty;
                if (string.IsNullOrEmpty(item) || ContainsString(destination, item))
                {
                    continue;
                }

                destination.Add(item);
            }
        }

        private static bool ContainsString(IReadOnlyList<string> values, string item)
        {
            if (values == null || string.IsNullOrEmpty(item))
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == item)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddStringRange(string[] source, List<string> destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            for (int i = 0; i < source.Length; i++)
            {
                string item = source[i] ?? string.Empty;
                if (string.IsNullOrEmpty(item))
                {
                    continue;
                }

                destination.Add(item);
            }
        }
    }
}
