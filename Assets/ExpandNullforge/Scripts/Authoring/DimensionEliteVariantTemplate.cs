using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// A tougher version of a creature, generated alongside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THIS IS NOT A SYSTEM. Core Keeper has no notion of an "elite" — no flag, no aura, no
    /// runtime upgrade path. Inventing one would mean a spawn-time hook, a stat-multiplier layer and a
    /// marker component, all to express something the game can already say: a second creature, rarer
    /// and stronger.
    /// </para>
    /// <para>
    /// So an elite is generated, not simulated. Turning this on produces a sibling prefab with the
    /// same behaviour and art, multiplied stats, and its own spawn row at a lower chance. In game it
    /// is simply another creature — which means every system that already works for creatures works
    /// for it, with nothing to keep in step.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionEliteVariantTemplate
    {
        [Tooltip("Also generate a rarer, tougher version of this creature.")]
        [SerializeField] private bool enabled;

        [Tooltip("Added to the creature's name for the elite. Leave blank to use 'Elite'.")]
        [SerializeField] private string namePrefix = "Elite";

        [Tooltip("Multiplies the creature's health.")]
        [Min(0.1f)]
        [SerializeField] private float healthMultiplier = 3f;

        [Tooltip("Multiplies the damage it deals.")]
        [Min(0f)]
        [SerializeField] private float damageMultiplier = 2f;

        [Tooltip("Adds to the flat damage it shrugs off.")]
        [Min(0)]
        [SerializeField] private int extraDamageReduction = 5;

        [Tooltip("Multiplies its movement speed. 1 keeps it the same.")]
        [Min(0.1f)]
        [SerializeField] private float speedMultiplier = 1.1f;

        [Tooltip("How much rarer than the ordinary creature it is. 10 means one tenth as common.")]
        [Min(1f)]
        [SerializeField] private float rarityFactor = 10f;

        [Tooltip("How many levels above the ordinary one it is. Only used when the creature's " +
                 "numbers come from the area level curve — with typed numbers, the multipliers " +
                 "above are what make it stronger. At most four.")]
        [Range(0, 4)]
        [SerializeField] private int extraLevels = 2;

        [Tooltip("Loot table for the elite. Leave empty to drop what the ordinary creature drops.")]
        [SerializeField] private DimensionLootTableAsset lootTable;

        public bool Enabled
        {
            get { return enabled; }
        }

        /// <summary>The word placed before the creature's name, never empty.</summary>
        /// <remarks>
        /// Falls back rather than allowing a blank, because an elite sharing its parent's exact name
        /// is indistinguishable in a death message, a bestiary or a loot log.
        /// </remarks>
        public string NamePrefix
        {
            get { return string.IsNullOrEmpty(namePrefix) ? "Elite" : namePrefix; }
        }

        public float HealthMultiplier
        {
            get { return healthMultiplier < 0.1f ? 0.1f : healthMultiplier; }
        }

        public float DamageMultiplier
        {
            get { return damageMultiplier < 0f ? 0f : damageMultiplier; }
        }

        public int ExtraDamageReduction
        {
            get { return extraDamageReduction < 0 ? 0 : extraDamageReduction; }
        }

        public float SpeedMultiplier
        {
            get { return speedMultiplier < 0.1f ? 0.1f : speedMultiplier; }
        }

        public float RarityFactor
        {
            get { return rarityFactor < 1f ? 1f : rarityFactor; }
        }

        /// <summary>How many levels above its parent a level-scaled elite stands.</summary>
        /// <remarks>
        /// Clamped to Core Keeper's own ceiling: <c>AreaLevelAuthoring.CalculateLevel</c> resolves
        /// a creature's level as <c>areaLevelNumber + (int)rarity</c>
        /// (`ck-db\Pug.Base\LevelScaling.cs:100`), and <c>Rarity</c> runs Common (0) to Legendary
        /// (4). This is the game's own way of saying "the same creature, a few levels harder", and
        /// it is the only one that keeps working as the player moves into deeper biomes.
        /// </remarks>
        public int ExtraLevels
        {
            get { return extraLevels < 0 ? 0 : (extraLevels > 4 ? 4 : extraLevels); }
        }

        public DimensionLootTableAsset LootTable
        {
            get { return lootTable; }
        }

        /// <summary>The elite's id, derived from its parent's.</summary>
        /// <remarks>
        /// Derived rather than authored so the two can never drift apart, and suffixed rather than
        /// prefixed so an elite sorts next to its parent everywhere ids are listed.
        /// </remarks>
        public static string IdFor(string creatureId)
        {
            return string.IsNullOrEmpty(creatureId) ? string.Empty : creatureId + ".elite";
        }

        /// <summary>The elite's display name.</summary>
        public string DisplayNameFor(string creatureDisplayName)
        {
            return string.IsNullOrEmpty(creatureDisplayName)
                ? NamePrefix
                : NamePrefix + " " + creatureDisplayName;
        }
    }
}
