using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The pin a boss shows on the map, the way the game's own bosses do.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In Core Keeper the pin is not on the boss: it is a separate placeable marker object that
    /// names the boss, visible from anywhere, that goes dark when the boss dies. This template
    /// authors that object; generation builds it beside the boss prefab and the author drops it
    /// into the arena.
    /// </para>
    /// <para>
    /// The icons are real sprites from your mod. Without one the pin still appears and still
    /// carries its name on hover — it just has no picture, which is worth a warning, not a
    /// refusal.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionBossMapPinTemplate
    {
        [Tooltip("It shows a pin on the map, the way the game's own bosses do.")]
        [SerializeField] private bool showsOnTheMap = true;

        [Tooltip("The picture on the full-screen map.")]
        [SerializeField] private Sprite largeMapIcon;

        [Tooltip("And the smaller one on the minimap.")]
        [SerializeField] private Sprite miniMapIcon;

        [Tooltip("The pin goes when the boss dies. Off keeps the pin forever, defeated or not.")]
        [SerializeField] private bool pinGoesWhenItDies = true;

        [Tooltip("What hovering the pin says. Empty uses the boss's name.")]
        [SerializeField] private string hoverName = string.Empty;

        public bool ShowsOnTheMap
        {
            get { return showsOnTheMap; }
        }

        public Sprite LargeMapIcon
        {
            get { return largeMapIcon; }
        }

        public Sprite MiniMapIcon
        {
            get { return miniMapIcon; }
        }

        public bool PinGoesWhenItDies
        {
            get { return pinGoesWhenItDies; }
        }

        public string HoverName
        {
            get { return hoverName ?? string.Empty; }
        }
    }
}
