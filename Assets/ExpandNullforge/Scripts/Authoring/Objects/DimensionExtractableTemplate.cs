using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Something a machine can pull resources out of, over and over, without destroying it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// On 66 vanilla objects. This is how the game's automated extractors work — a machine sits next
    /// to a thing and takes what that thing yields, on a timer, indefinitely. It is deliberately not
    /// loot: loot is what falls out when something breaks, and an extractable is never consumed.
    /// </para>
    /// <para>
    /// A resource node that a modder wants an automated base built around needs this and nothing
    /// else. Without it the only way to get anything out of a custom node is to break it.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionExtractableTemplate
    {
        [Tooltip("What a machine gets out of it. Empty means machines get nothing.")]
        [SerializeField] private DimensionExtractableOutput[] yields = new DimensionExtractableOutput[0];

        [Tooltip("How long one extraction takes, overriding the machine's own time. 0 keeps the machine's.")]
        [Min(0)]
        [SerializeField] private int extractionSecondsOverride;

        public DimensionExtractableOutput[] Yields
        {
            get { return yields ?? new DimensionExtractableOutput[0]; }
        }

        public int ExtractionSecondsOverride
        {
            get { return extractionSecondsOverride < 0 ? 0 : extractionSecondsOverride; }
        }

        /// <summary>Whether a machine could pull anything at all out of this.</summary>
        public bool YieldsAnything { get { return Yields.Length > 0; } }

        /// <summary>Whether an extraction time was set on something machines get nothing from.</summary>
        public bool ExtractionTimeWillBeIgnored
        {
            get { return extractionSecondsOverride > 0 && !YieldsAnything; }
        }
    }

    /// <summary>One thing a machine pulls out, and how much of it.</summary>
    [Serializable]
    public struct DimensionExtractableOutput
    {
        [Tooltip("What comes out.")]
        [SerializeField] private string objectId;

        [Tooltip("Which look of it.")]
        [Min(0)]
        [SerializeField] private int variation;

        [Tooltip("Fewest and most that come out per extraction. 0,0 uses the machine's own amount.")]
        [SerializeField] private Vector2 amountRange;

        public string ObjectId { get { return objectId ?? string.Empty; } }

        public int Variation { get { return variation < 0 ? 0 : variation; } }

        /// <summary>The amount range, never with the high end below the low end.</summary>
        public Vector2 AmountRange
        {
            get
            {
                float low = amountRange.x < 0f ? 0f : amountRange.x;
                float high = amountRange.y < 0f ? 0f : amountRange.y;
                return new Vector2(low, high < low ? low : high);
            }
        }
    }
}
