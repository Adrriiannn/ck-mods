using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>An extra item a crop version gives on top of its harvest.</summary>
    [Serializable]
    public sealed class DimensionCropVersionDropTemplate
    {
        [Tooltip("What else picking it gives. An ObjectID name, or the full name of one of your own items.")]
        [SerializeField] private string itemId = string.Empty;

        [Tooltip("How many of it.")]
        [Min(1)]
        [SerializeField] private int amount = 1;

        public string ItemId
        {
            get { return itemId ?? string.Empty; }
        }

        public int Amount
        {
            get { return Mathf.Max(1, amount); }
        }

        public bool IsEmpty
        {
            get { return string.IsNullOrEmpty(ItemId); }
        }
    }

    /// <summary>
    /// A better version of a crop — the thing players call golden, and any rarer tier above it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CORE KEEPER HAS ONE OF THESE AND CALLS IT "RARE". A golden crop is not a separate object: it
    /// is the same plant on a different <em>variation</em>, and that variation's own prefab carries
    /// its own produce and its own drops. Variations are ordinary integers, so a second and a third
    /// version cost nothing structurally — what the game does not have is a place to say how rare
    /// each one is, and a way to grow a seed of the third version into the plant of the third
    /// version. The framework supplies both, so this list can be any length.
    /// </para>
    /// <para>
    /// THE FIRST VERSION IN THE LIST IS THE GOLDEN SLOT. It sits on the variations Core Keeper's own
    /// golden crops use, which is why only it can hand its roll back to the game with
    /// <see cref="UsesTheGamesGoldenChance"/> — the game has exactly one such roll, and it places
    /// exactly one variation.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCropVersionTemplate
    {
        /// <summary>What Core Keeper's own golden crops come up at, out of a hundred plantings.</summary>
        /// <remarks>
        /// <c>Constants.baseChanceToGainRarePlantPercentage</c>. A player's gardening talents raise
        /// it as far as eighteen; the framework's own versions get no such help, so this is the
        /// number to compare a new version against.
        /// </remarks>
        public const float VanillaGoldenChancePercent = 3f;

        [Tooltip("What this version is called. Used to name its prefabs, so keep it short.")]
        [SerializeField] private string versionName = "Golden";

        [Tooltip("Out of every hundred plantings, how many come up as this version.")]
        [Min(0f)]
        [SerializeField] private float chancePercent = VanillaGoldenChancePercent;

        [Tooltip("Let the game roll this one, at its own golden chance plus the player's gardening bonuses. Only the first version can.")]
        [SerializeField] private bool usesTheGamesGoldenChance = true;

        [Tooltip("What picking this version gives. Leave empty to give the ordinary harvest.")]
        [SerializeField] private string produceItemId = string.Empty;

        [Tooltip("How many of that a harvest gives. Leave at zero to give as many as the ordinary plant.")]
        [Min(0)]
        [SerializeField] private int harvestAmount;

        [Tooltip("Extra items picking this version also hands over.")]
        [SerializeField] private DimensionCropVersionDropTemplate[] extraDrops =
            new DimensionCropVersionDropTemplate[0];

        [Tooltip("How often you get the seed and the extras back, out of a hundred picks. Zero uses the plant's own number.")]
        [Min(0f)]
        [SerializeField] private float chanceToGetThingsBackPercent;

        [Tooltip("What this version looks like. Anything left empty is drawn the same as the ordinary crop.")]
        [SerializeField] private DimensionCropVersionLookTemplate look =
            new DimensionCropVersionLookTemplate();

        [SerializeField] private bool enabled = true;

        public string VersionName
        {
            get { return versionName ?? string.Empty; }
        }

        public float ChancePercent
        {
            get { return chancePercent < 0f ? 0f : chancePercent; }
        }

        public bool UsesTheGamesGoldenChance
        {
            get { return usesTheGamesGoldenChance; }
        }

        public string ProduceItemId
        {
            get { return produceItemId ?? string.Empty; }
        }

        /// <summary>How many a harvest gives, or zero to mean "same as the ordinary plant".</summary>
        public int HarvestAmount
        {
            get { return harvestAmount < 0 ? 0 : harvestAmount; }
        }

        public DimensionCropVersionDropTemplate[] ExtraDrops
        {
            get { return extraDrops ?? new DimensionCropVersionDropTemplate[0]; }
        }

        /// <summary>Out of a hundred picks, or zero to mean "same as the ordinary plant".</summary>
        public float ChanceToGetThingsBackPercent
        {
            get { return chanceToGetThingsBackPercent < 0f ? 0f : chanceToGetThingsBackPercent; }
        }

        /// <summary>What this version looks like. Never null.</summary>
        public DimensionCropVersionLookTemplate Look
        {
            get { return look ?? (look = new DimensionCropVersionLookTemplate()); }
        }

        public bool Enabled
        {
            get { return enabled; }
        }

        /// <summary>Whether it is rolled by the framework and can never come up.</summary>
        public bool NeverComesUp
        {
            get { return !UsesTheGamesGoldenChance && ChancePercent <= 0f; }
        }
    }
}
