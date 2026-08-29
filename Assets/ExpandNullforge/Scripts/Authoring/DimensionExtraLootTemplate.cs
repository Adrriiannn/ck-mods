using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The three ways something drops loot that are not "it died".
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>DropLootAuthoring</c> has four channels and the framework only reached one. The other
    /// three are how Core Keeper does some of its most recognisable objects:
    /// </para>
    /// <list type="bullet">
    /// <item><b>As it is hit</b> — the ore vein that sheds ore before it breaks, the tree that drops
    /// wood as you chop. Loot on a damage threshold rather than on death.</item>
    /// <item><b>When it is used</b> — a container or shrine that gives something on interaction
    /// without being destroyed.</item>
    /// <item><b>By season</b> — the same object dropping different things during an event.</item>
    /// </list>
    /// <para>
    /// EACH CHANNEL IS PAIRED WITH ITS OWN "has" FLAG on the component, and the game reads the flag
    /// rather than checking whether the data is empty. Filling in the drops without the flag is
    /// silent and total: nothing drops and nothing says why. The flags are set from whether the
    /// author actually filled the section in, so that failure cannot be authored.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionExtraLootTemplate
    {
        [Header("As it is hit")]
        [Tooltip("It drops something each time it takes enough damage, before it breaks.")]
        [SerializeField] private bool dropsLootAsItIsHit;

        [Tooltip("What it sheds. One of the game's objects, or one of yours. Empty for nothing.")]
        [SerializeField] private string shedsObjectId = string.Empty;

        [Tooltip("How much damage has to land before it sheds once.")]
        [Min(0)]
        [SerializeField] private int damageNeededToShed;

        [Tooltip("Or, the share of its health that damage has to add up to.")]
        [Range(0f, 1f)]
        [SerializeField] private float healthShareNeededToShed;

        [Tooltip("It stops shedding once its health drops below this many points.")]
        [Min(0)]
        [SerializeField] private int stopsSheddingBelowHealth;

        [Tooltip("Or below this share of its health.")]
        [Range(0f, 1f)]
        [SerializeField] private float stopsSheddingBelowHealthShare;

        [Tooltip("What it sheds appears as a live object rather than a pickup.")]
        [SerializeField] private bool shedsALiveObject;

        [Tooltip("Nearest corner of where the shed thing lands, relative to it.")]
        [SerializeField] private Vector2 shedLandsFrom = Vector2.zero;

        [Tooltip("Furthest corner of where it lands.")]
        [SerializeField] private Vector2 shedLandsTo = Vector2.zero;

        [Tooltip("It stops shedding once this many are already lying nearby. 0 for no limit.")]
        [Min(0)]
        [SerializeField] private int stopsAfterThisManyNearby;

        [Header("When it is used")]
        [Tooltip("It gives something when a player uses it, without being destroyed.")]
        [SerializeField] private bool dropsLootWhenUsed;

        [Tooltip("A loot table it rolls on use. Blank to use the list below instead.")]
        [SerializeField] private string useLootTableId = string.Empty;

        [Tooltip("An effect played as it gives. Blank for none.")]
        [SerializeField] private string useEffectId = string.Empty;

        [Tooltip("Exactly what it gives on use.")]
        [SerializeField] private DimensionLootEntry[] givesOnUse = new DimensionLootEntry[0];

        [Header("By season")]
        [Tooltip("It drops different things during a season or event.")]
        [SerializeField] private bool dropsDifferentLootInSeason;

        [Tooltip("Which season, by the game's own name.")]
        [SerializeField] private string season = string.Empty;

        [Tooltip("What it drops during that season.")]
        [SerializeField] private DimensionLootEntry[] seasonalDrops = new DimensionLootEntry[0];

        public bool DropsLootAsItIsHit { get { return dropsLootAsItIsHit; } }

        public string ShedsObjectId { get { return shedsObjectId ?? string.Empty; } }

        public int DamageNeededToShed
        {
            get { return damageNeededToShed < 0 ? 0 : damageNeededToShed; }
        }

        public float HealthShareNeededToShed { get { return Clamp01(healthShareNeededToShed); } }

        public int StopsSheddingBelowHealth
        {
            get { return stopsSheddingBelowHealth < 0 ? 0 : stopsSheddingBelowHealth; }
        }

        public float StopsSheddingBelowHealthShare
        {
            get { return Clamp01(stopsSheddingBelowHealthShare); }
        }

        public bool ShedsALiveObject { get { return shedsALiveObject; } }

        public Vector2 ShedLandsFrom { get { return shedLandsFrom; } }

        public Vector2 ShedLandsTo { get { return shedLandsTo; } }

        public int StopsAfterThisManyNearby
        {
            get { return stopsAfterThisManyNearby < 0 ? 0 : stopsAfterThisManyNearby; }
        }

        public bool DropsLootWhenUsed { get { return dropsLootWhenUsed; } }

        public string UseLootTableId { get { return useLootTableId ?? string.Empty; } }

        public string UseEffectId { get { return useEffectId ?? string.Empty; } }

        public DimensionLootEntry[] GivesOnUse
        {
            get { return givesOnUse ?? new DimensionLootEntry[0]; }
        }

        public bool DropsDifferentLootInSeason { get { return dropsDifferentLootInSeason; } }

        /// <summary>
        /// Whether any of the three extra channels is asked for at all.
        /// </summary>
        /// <remarks>
        /// Asked by every caller that has to decide whether the loot component may be taken off
        /// again. A creature with no death loot table but something it sheds as it is hit is an
        /// ordinary thing, and it used to lose the component a moment after this pass added it.
        /// </remarks>
        public bool WantsAnExtraChannel
        {
            get
            {
                return DropsLootAsItIsHit || DropsLootWhenUsed || DropsDifferentLootInSeason;
            }
        }

        public string Season { get { return season ?? string.Empty; } }

        public DimensionLootEntry[] SeasonalDrops
        {
            get { return seasonalDrops ?? new DimensionLootEntry[0]; }
        }

        /// <summary>Whether it is set to shed as it is hit with no threshold to shed at.</summary>
        /// <remarks>
        /// Both thresholds at zero means every point of damage sheds, which is almost never what
        /// someone meant and reads in-game as an object vomiting loot.
        /// </remarks>
        public bool ShedsWithNoThreshold
        {
            get
            {
                return dropsLootAsItIsHit
                    && damageNeededToShed <= 0
                    && healthShareNeededToShed <= 0f;
            }
        }

        /// <summary>Whether it is set to shed without naming what it sheds.</summary>
        public bool ShedsNothing
        {
            get { return dropsLootAsItIsHit && string.IsNullOrEmpty(ShedsObjectId); }
        }

        /// <summary>Whether a use-drop was asked for with neither a table nor a list.</summary>
        public bool GivesNothingOnUse
        {
            get
            {
                return dropsLootWhenUsed
                    && string.IsNullOrEmpty(UseLootTableId)
                    && GivesOnUse.Length == 0;
            }
        }

        /// <summary>Whether a season was named with nothing to drop during it.</summary>
        public bool SeasonHasNoDrops
        {
            get { return dropsDifferentLootInSeason && SeasonalDrops.Length == 0; }
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }
    }

    /// <summary>One thing dropped, how many, and how likely.</summary>
    [Serializable]
    public struct DimensionLootEntry
    {
        [Tooltip("What drops.")]
        [SerializeField] private string objectId;

        [Tooltip("How many.")]
        [Min(0)]
        [SerializeField] private int amount;

        [Tooltip("How likely, from 0 to 1.")]
        [Range(0f, 1f)]
        [SerializeField] private float chance;

        [Tooltip("Extra amount added per additional player, for seasonal drops.")]
        [Min(0f)]
        [SerializeField] private float extraPerPlayer;

        public string ObjectId { get { return objectId ?? string.Empty; } }

        public int Amount { get { return amount < 0 ? 0 : amount; } }

        public float Chance
        {
            get
            {
                if (chance < 0f)
                {
                    return 0f;
                }

                return chance > 1f ? 1f : chance;
            }
        }

        public float ExtraPerPlayer { get { return extraPerPlayer < 0f ? 0f : extraPerPlayer; } }
    }
}
