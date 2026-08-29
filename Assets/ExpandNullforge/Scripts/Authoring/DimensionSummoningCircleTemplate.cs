using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A place where putting the right item down brings something out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>SummonAreaAuthoring</c>, on 33 vanilla objects. Every boss arena entrance in the game is
    /// one of these: an altar that watches for its summoning item, clears the ground, and brings the
    /// boss in. A custom boss with no way to be summoned can only be spawned by console.
    /// </para>
    /// <para>
    /// The two distance overrides are the fiddly part and both matter. One is how far it looks for
    /// its summoning item — too small and the item has to be placed exactly. The other is how far it
    /// looks for a boss that is already out, which is what stops a player summoning five at once.
    /// </para>
    /// <para>
    /// NOT OFFERED: <c>internalState</c> and <c>internalTimer</c> are the component's own running
    /// state, written by the game while the summon plays out.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionSummoningCircleTemplate
    {
        [Tooltip("What arrives when the right item is put down here. Blank for nothing.")]
        [SerializeField] private string summonsObjectId = string.Empty;

        [Tooltip("A second thing it can summon instead. Blank for none.")]
        [SerializeField] private string alternativeObjectId = string.Empty;

        [Tooltip("How long the summon builds before anything appears.")]
        [Min(0f)]
        [SerializeField] private float windUpSeconds = 2f;

        [Tooltip("How long the arrival itself takes.")]
        [Min(0f)]
        [SerializeField] private float arrivalSeconds = 1f;

        [Tooltip("How much ground is cleared as it arrives, in tiles. 0 clears nothing.")]
        [Min(0)]
        [SerializeField] private int clearsTilesWithin;

        [Tooltip("Where it appears relative to the circle.")]
        [SerializeField] private Vector3 arrivesAt = Vector3.zero;

        [Tooltip("The summoning item stays where the player put it rather than being centred.")]
        [SerializeField] private bool summoningItemStaysWhereItWasPut;

        [Tooltip("How far it looks for its summoning item. 0 uses the game's own distance.")]
        [Min(0f)]
        [SerializeField] private float looksForItsItemWithin;

        [Tooltip("How far it looks for one already out, so a player cannot summon several. 0 uses the game's own.")]
        [Min(0f)]
        [SerializeField] private float looksForAnExistingOneWithin;

        public string SummonsObjectId { get { return summonsObjectId ?? string.Empty; } }

        public string AlternativeObjectId { get { return alternativeObjectId ?? string.Empty; } }

        public float WindUpSeconds { get { return windUpSeconds < 0f ? 0f : windUpSeconds; } }

        public float ArrivalSeconds { get { return arrivalSeconds < 0f ? 0f : arrivalSeconds; } }

        public int ClearsTilesWithin { get { return clearsTilesWithin < 0 ? 0 : clearsTilesWithin; } }

        public Vector3 ArrivesAt { get { return arrivesAt; } }

        public bool SummoningItemStaysWhereItWasPut
        {
            get { return summoningItemStaysWhereItWasPut; }
        }

        public float LooksForItsItemWithin
        {
            get { return looksForItsItemWithin < 0f ? 0f : looksForItsItemWithin; }
        }

        public float LooksForAnExistingOneWithin
        {
            get { return looksForAnExistingOneWithin < 0f ? 0f : looksForAnExistingOneWithin; }
        }

        /// <summary>Whether this is a summoning circle at all.</summary>
        public bool SummonsSomething { get { return !string.IsNullOrEmpty(SummonsObjectId); } }

        /// <summary>
        /// Whether it names an alternative to summon without naming a first one.
        /// </summary>
        /// <remarks>
        /// The alternative is the second choice, not a standalone. With no first one the component
        /// is never attached, so the alternative is never read either.
        /// </remarks>
        public bool AlternativeWithoutAPrimary
        {
            get
            {
                return !string.IsNullOrEmpty(AlternativeObjectId)
                    && string.IsNullOrEmpty(SummonsObjectId);
            }
        }
    }
}
