using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The small roles an object can play in a base: something you sit on, something the paint tool
    /// recognises, something you can find, something nothing can hit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Six vanilla components with one or no settings each, asked about together because separately
    /// they would each be a panel with a single tickbox in it. All of them are the difference
    /// between an object that looks right and an object that behaves right.
    /// </para>
    /// <para>
    /// <c>SittableAuthoring</c> (36 objects), <c>NameAuthoring</c> (36) and
    /// <c>NonHittableAuthoring</c> (27) hold nothing at all — the component's presence IS the
    /// setting. <c>PaintToolAuthoring</c> (42), <c>ResizableTileSizeAuthoring</c> (39) and
    /// <c>CanBeDiscoveredAuthoring</c> (42) hold one value each.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionObjectRolesTemplate
    {
        [Header("Living with it")]
        [Tooltip("A player can sit on it.")]
        [SerializeField] private bool canBeSatOn;

        [Tooltip("It carries a name a player can set — the way a pet or a sign does.")]
        [SerializeField] private bool canBeNamed;

        [Tooltip("The paint tool works on it.")]
        [SerializeField] private bool canBePainted;

        [Tooltip("Which paint it starts as. 0 is unpainted.")]
        [Min(0)]
        [SerializeField] private int startingPaint;

        [Header("Its size on the floor")]
        [Tooltip("Its footprint can be changed after it is placed.")]
        [SerializeField] private bool sizeCanBeChanged;

        [Tooltip("It starts at its smallest size rather than its largest.")]
        [SerializeField] private bool startsSmallest = true;

        [Header("Finding it")]
        [Tooltip("Walking near it counts as discovering it, the way a landmark does.")]
        [SerializeField] private bool isADiscovery;

        [Tooltip("How close a player has to get to discover it. The game's own value is 5.")]
        [Min(0f)]
        [SerializeField] private float discoveredWithin = 5f;

        [Header("Being hit")]
        [Tooltip("Nothing can hit it at all — attacks pass straight through.")]
        [SerializeField] private bool nothingCanHitIt;

        [Tooltip("It starts immune to damage, or explicitly vulnerable. Leave alone for neither.")]
        [SerializeField] private DimensionDamageImmunity startsImmune = DimensionDamageImmunity.LeaveItAlone;

        [Tooltip("An effect shown when something is turned away by that immunity. Blank for the usual.")]
        [SerializeField] private string turnedAwayEffectId = string.Empty;

        public bool CanBeSatOn { get { return canBeSatOn; } }

        public bool CanBeNamed { get { return canBeNamed; } }

        public bool CanBePainted { get { return canBePainted; } }

        public int StartingPaint { get { return startingPaint < 0 ? 0 : startingPaint; } }

        public bool SizeCanBeChanged { get { return sizeCanBeChanged; } }

        public bool StartsSmallest { get { return startsSmallest; } }

        public bool IsADiscovery { get { return isADiscovery; } }

        public float DiscoveredWithin
        {
            get { return discoveredWithin < 0f ? 0f : discoveredWithin; }
        }

        public bool NothingCanHitIt { get { return nothingCanHitIt; } }

        public DimensionDamageImmunity StartsImmune { get { return startsImmune; } }

        public string TurnedAwayEffectId { get { return turnedAwayEffectId ?? string.Empty; } }

        /// <summary>Whether a paint colour was chosen on something the paint tool ignores.</summary>
        public bool PaintWillBeIgnored
        {
            get { return !canBePainted && startingPaint > 0; }
        }

        /// <summary>Whether a discovery distance was set on something that is not a discovery.</summary>
        public bool DiscoveryDistanceWillBeIgnored
        {
            get { return !isADiscovery && discoveredWithin != 5f; }
        }
    }

    /// <summary>
    /// Whether an object starts able to be hurt. Core Keeper's <c>ImmuneToDamageState</c>.
    /// </summary>
    /// <remarks>
    /// The game's own enum starts at -1 for "not set", which is why leaving it alone is a real
    /// choice here rather than an absence — an object told explicitly that it is vulnerable behaves
    /// differently from one never asked.
    /// </remarks>
    public enum DimensionDamageImmunity
    {
        /// <summary>Never asked. <c>Invalid</c>, the game's own -1.</summary>
        LeaveItAlone = -1,

        /// <summary>Explicitly able to be hurt. <c>Vulnerable</c>.</summary>
        CanBeHurt = 0,

        /// <summary>Starts immune until something changes it. <c>Immune</c>.</summary>
        StartsImmune = 1
    }
}
