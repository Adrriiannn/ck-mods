using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A named place inside your dimension: its own title card, ambience and music, carried by
    /// its signature blocks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE GAME HAS NO "AREA" OBJECT, and this asset is honest about that. What Core Keeper
    /// calls the Meadow or the Larva Hive is nothing but tiles: three separate systems count
    /// the blocks near the player — one to pick the music, one to mix the ambience, one to
    /// show the title card — and the "area" exists wherever enough of its blocks stand. This
    /// asset fans one name out to all three registries, so building with the area's blocks IS
    /// placing the area, whether by hand, by a dungeon's fill, or by a scene.
    /// </para>
    /// <para>
    /// That is also the honest limit: the area has no border. Walk where the blocks thin out
    /// and it fades, exactly as vanilla's own sub-biomes do.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Dimensions API/Named Area")]
    public sealed class DimensionNamedAreaAsset : ScriptableObject
    {
        [SerializeField] private string areaId = "area";
        [SerializeField] private string displayName = "Named Area";

        [Tooltip("The blocks that ARE this area. Where enough of them stand, the area exists.")]
        [SerializeField] private DimensionTilesetAsset[] blocks = new DimensionTilesetAsset[0];

        [Tooltip("Its name appears across the screen when a player first walks in.")]
        [SerializeField] private bool showTitleOnDiscovery = true;

        [Tooltip("The title card's color.")]
        [SerializeField] private Color titleColor = Color.white;

        [Tooltip("An object whose icon appears beside the title. Empty shows none.")]
        [SerializeField] private string titleIconObjectId = string.Empty;

        [Tooltip("What it sounds like: a sound clip key, looping as its ambience. Empty keeps " +
                 "the surrounding ambience.")]
        [SerializeField] private string ambienceSoundKey = string.Empty;

        [Range(0f, 1f)]
        [SerializeField] private float ambienceVolume = 0.6f;

        [Tooltip("What plays here: a game music roster name (BOSS, MYSTERY, MOLD_DUNGEON...) " +
                 "or one of your own music cues. Empty keeps the surrounding music.")]
        [SerializeField] private string musicRosterName = string.Empty;

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string AreaId { get { return areaId ?? string.Empty; } }

        public string DisplayName { get { return displayName ?? string.Empty; } }

        public DimensionTilesetAsset[] Blocks
        {
            get { return blocks ?? new DimensionTilesetAsset[0]; }
        }

        public bool ShowTitleOnDiscovery { get { return showTitleOnDiscovery; } }

        public Color TitleColor { get { return titleColor; } }

        public string TitleIconObjectId { get { return titleIconObjectId ?? string.Empty; } }

        public string AmbienceSoundKey { get { return ambienceSoundKey ?? string.Empty; } }

        public float AmbienceVolume { get { return Mathf.Clamp01(ambienceVolume); } }

        public string MusicRosterName { get { return musicRosterName ?? string.Empty; } }

        public bool Enabled { get { return enabled; } }

        public string Notes { get { return notes ?? string.Empty; } }
    }
}
