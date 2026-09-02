using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The remaining roles an object can hold in the world: shrines, summoning items, fireflies,
    /// graves, fire that spreads, barriers, and the rest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nine of these vanilla components hold no settings whatsoever — their presence is the whole
    /// behaviour. Grouping them means one panel with a column of plain statements rather than nine
    /// panels each containing a single tickbox.
    /// </para>
    /// <para>
    /// THE SUMMONING ITEM IS THE OTHER HALF OF A SUMMONING CIRCLE. The circle watches for an item;
    /// this is the item, and it names which bosses it is good for. A custom boss needs both ends or
    /// it can only be spawned by console.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionWorldRolesTemplate
    {
        [Header("Summoning")]
        [Tooltip("It is a summoning item — putting it on the right circle brings something out.")]
        [SerializeField] private bool isASummoningItem;

        [Tooltip("Which bosses it can summon. The game's own, or your own bosses by their ids.")]
        [SerializeField] private string[] summonsAnyOf = new string[0];

        [Tooltip("It is a shrine that a titan is bound to.")]
        [SerializeField] private bool isATitanShrine;

        [Tooltip("Which titan that is. One of the game's, or one of your own creatures.")]
        [SerializeField] private string boundTitanId = string.Empty;

        [Header("Ambient life and effects")]
        [Tooltip("It is a firefly — ambient drifting light.")]
        [SerializeField] private bool isAFirefly;

        [Tooltip("It spreads fire to what is around it.")]
        [SerializeField] private bool spreadsFire;

        [Tooltip("It becomes a tile when it settles, rather than staying an object.")]
        [SerializeField] private bool becomesGroundWhenItSettles;

        [Tooltip("It is a root — part of the growing tangle rather than a plant in its own right.")]
        [SerializeField] private bool isARoot;

        [Header("Barriers and zones")]
        [Tooltip("It is a mana barrier — it blocks the way until it is dealt with.")]
        [SerializeField] private bool isAManaBarrier;

        [Tooltip("It switches off any immunity zone covering it.")]
        [SerializeField] private bool switchesOffImmunityZones;

        [Tooltip("How far its aura reaches, overriding the usual. 0 keeps the usual.")]
        [Min(0f)]
        [SerializeField] private float auraReachOverride;

        [Header("Homes and remains")]
        [Tooltip("A creature can make its home here — an idol something lives around.")]
        [SerializeField] private bool creaturesCanLiveHere;

        [Tooltip("It is a player's grave, holding what they dropped.")]
        [SerializeField] private bool isAPlayerGrave;

        [Tooltip("It carries an affix — a rolled modifier, the way rare gear does.")]
        [SerializeField] private bool carriesAnAffix;

        [Header("As a weapon or minion")]
        [Tooltip("Holding it lets the player keep moving at full speed while attacking.")]
        [SerializeField] private bool letsThePlayerKeepMoving;

        [Tooltip("How fast they move while holding it. 1 is normal speed.")]
        [Min(0f)]
        [SerializeField] private float movingSpeedMultiplier = 1f;

        [Tooltip("It is a minion — a summoned helper that fights for its owner.")]
        [SerializeField] private bool isAMinion;

        [Tooltip("How hard the minion hits for its tier. 1 is the baseline.")]
        [Min(0f)]
        [SerializeField] private float minionHitsThisHardForItsTier = 1f;

        [Tooltip("The minion mines as well as fights.")]
        [SerializeField] private bool minionMinesToo;

        [Tooltip("How hard the minion mines for its tier.")]
        [Min(0f)]
        [SerializeField] private float minionMinesThisHardForItsTier = 1f;

        public bool IsASummoningItem { get { return isASummoningItem; } }

        public string[] SummonsAnyOf { get { return summonsAnyOf ?? new string[0]; } }

        public bool IsATitanShrine { get { return isATitanShrine; } }

        public string BoundTitanId { get { return boundTitanId ?? string.Empty; } }

        public bool IsAFirefly { get { return isAFirefly; } }

        public bool SpreadsFire { get { return spreadsFire; } }

        public bool BecomesGroundWhenItSettles { get { return becomesGroundWhenItSettles; } }

        public bool IsARoot { get { return isARoot; } }

        public bool IsAManaBarrier { get { return isAManaBarrier; } }

        public bool SwitchesOffImmunityZones { get { return switchesOffImmunityZones; } }

        public float AuraReachOverride
        {
            get { return auraReachOverride < 0f ? 0f : auraReachOverride; }
        }

        public bool CreaturesCanLiveHere { get { return creaturesCanLiveHere; } }

        public bool IsAPlayerGrave { get { return isAPlayerGrave; } }

        public bool CarriesAnAffix { get { return carriesAnAffix; } }

        public bool LetsThePlayerKeepMoving { get { return letsThePlayerKeepMoving; } }

        public float MovingSpeedMultiplier
        {
            get { return movingSpeedMultiplier < 0f ? 0f : movingSpeedMultiplier; }
        }

        public bool IsAMinion { get { return isAMinion; } }

        public float MinionHitsThisHardForItsTier
        {
            get
            {
                return minionHitsThisHardForItsTier < 0f ? 0f : minionHitsThisHardForItsTier;
            }
        }

        public bool MinionMinesToo { get { return minionMinesToo; } }

        public float MinionMinesThisHardForItsTier
        {
            get
            {
                return minionMinesThisHardForItsTier < 0f ? 0f : minionMinesThisHardForItsTier;
            }
        }

        /// <summary>Whether it is a summoning item that summons nothing.</summary>
        public bool SummonsNothing
        {
            get { return isASummoningItem && SummonsAnyOf.Length == 0; }
        }

        /// <summary>Whether it is a shrine with no titan bound to it.</summary>
        public bool ShrineHasNoTitan
        {
            get { return isATitanShrine && string.IsNullOrEmpty(BoundTitanId); }
        }

        /// <summary>Whether a mining rate was set on a minion that does not mine.</summary>
        public bool MinionMiningWillBeIgnored
        {
            get { return isAMinion && !minionMinesToo && minionMinesThisHardForItsTier != 1f; }
        }
    }
}
