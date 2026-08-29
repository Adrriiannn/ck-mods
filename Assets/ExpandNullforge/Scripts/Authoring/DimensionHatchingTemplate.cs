using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The creature is an egg: a player coming near makes it hatch into something else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Exactly how the Larva Hive's cocoons work — an ordinary creature carrying one
    /// component. A player within five tiles starts the hatch; after the wind-up, the brood
    /// scatters out and the shell is left at a sliver of health. Walk away before it finishes
    /// (ten tiles) and it settles back down.
    /// </para>
    /// <para>
    /// The distances and the scatter are the game's own fixed numbers, compiled into its
    /// hatching job — they are stated here as facts, not offered as knobs, because pretending
    /// otherwise would be configuration that does nothing.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionHatchingTemplate
    {
        [Tooltip("It is an egg: a player coming near makes it hatch.")]
        [SerializeField] private bool hatches;

        [Tooltip("What crawls out — one of your creatures by id, or a vanilla one by name.")]
        [SerializeField] private string whatHatchesOut = string.Empty;

        [Tooltip("Fewest and most that crawl out.")]
        [SerializeField] private Vector2Int howMany = new Vector2Int(3, 9);

        [Tooltip("The wind-up, in seconds, once a player is near.")]
        [Min(0.1f)]
        [SerializeField] private float secondsToHatch = 3f;

        public bool Hatches
        {
            get { return hatches && !string.IsNullOrEmpty(WhatHatchesOut); }
        }

        public string WhatHatchesOut { get { return whatHatchesOut ?? string.Empty; } }

        public Vector2Int HowMany
        {
            get
            {
                int min = Mathf.Max(1, howMany.x);
                return new Vector2Int(min, Mathf.Max(min, howMany.y));
            }
        }

        public float SecondsToHatch { get { return secondsToHatch < 0.1f ? 0.1f : secondsToHatch; } }
    }
}
