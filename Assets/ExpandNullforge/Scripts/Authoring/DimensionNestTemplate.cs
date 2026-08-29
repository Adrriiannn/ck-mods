using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Something that keeps producing creatures or objects around itself — a nest, a hive, a
    /// spawner.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>SpawnAroundObjectAuthoring</c> is how the game populates a place rather than an instant.
    /// A larva hive, a slime territory, the critters that appear near water: all of them are this,
    /// and every one of its settings is about restraint — how many at once, how far out, how often,
    /// and under what conditions it stops.
    /// </para>
    /// <para>
    /// A SPAWNER WITHOUT A LIMIT IS A CRASH. <c>limitNumberSpawned</c> defaults to 50 in the game
    /// for that reason, and a custom nest set to zero would keep producing forever. That is enforced
    /// rather than left as a warning.
    /// </para>
    /// <para>
    /// A nest can hold several entries, each with its own creature, rate and conditions, so one hive
    /// can produce common larvae constantly and something rarer occasionally.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionNestTemplate
    {
        [Tooltip("It keeps producing things around itself.")]
        [SerializeField] private bool isANest;

        [Tooltip("What it produces. Each entry has its own rate and conditions.")]
        [SerializeField] private DimensionNestBrood[] broods = new DimensionNestBrood[0];

        public bool IsANest { get { return isANest; } }

        public DimensionNestBrood[] Broods
        {
            get { return broods ?? new DimensionNestBrood[0]; }
        }

        /// <summary>Whether it is a nest that produces nothing.</summary>
        public bool ProducesNothing { get { return isANest && Broods.Length == 0; } }
    }

    /// <summary>One thing a nest produces, and the restraint on it.</summary>
    [Serializable]
    public struct DimensionNestBrood
    {
        [Tooltip("What appears.")]
        [SerializeField] private string objectId;

        [Tooltip("Which look of it.")]
        [Min(0)]
        [SerializeField] private int variation;

        [Tooltip("How many exist at once before it stops. Never leave this at zero.")]
        [Min(1)]
        [SerializeField] private int atMostAtOnce;

        [Tooltip("Shortest wait between appearances.")]
        [Min(0f)]
        [SerializeField] private float minWait;

        [Tooltip("Longest wait between appearances.")]
        [Min(0f)]
        [SerializeField] private float maxWait;

        [Tooltip("How far out from the nest they appear.")]
        [Min(0f)]
        [SerializeField] private float appearsWithin;

        [Tooltip("Shortest wait once the limit has been reached.")]
        [Min(0f)]
        [SerializeField] private float minWaitWhenFull;

        [Tooltip("Longest wait once the limit has been reached.")]
        [Min(0f)]
        [SerializeField] private float maxWaitWhenFull;

        [Header("When it produces")]
        [Tooltip("It only produces while the nest is in a fight.")]
        [SerializeField] private bool onlyWhileFighting;

        [Tooltip("They appear near players rather than anywhere around the nest.")]
        [SerializeField] private bool appearsNearPlayers;

        [Tooltip("Biomes it will produce in, by the game's own names. Empty means anywhere.")]
        [SerializeField] private string[] onlyInBiomes;

        [Tooltip("The player has to be standing in one of those biomes too.")]
        [SerializeField] private bool playerMustBeInThatBiome;

        [Tooltip("It only produces during this season. Blank for all year.")]
        [SerializeField] private string onlyInSeason;

        [Tooltip("A condition the nest must be under to produce. Blank for none.")]
        [SerializeField] private string requiresCondition;

        [Tooltip("It avoids producing close to this object. Blank for none.")]
        [SerializeField] private string keepsAwayFrom;

        [Header("Kind of thing")]
        [Tooltip("They are critters — ambient life that despawns — rather than real objects.")]
        [SerializeField] private bool producesCritters;

        [Tooltip("How far a critter gets from a player before it despawns.")]
        [Min(0f)]
        [SerializeField] private float critterDespawnDistance;

        [Tooltip("What it produces sticks around when nobody is looking.")]
        [SerializeField] private bool whatItMakesPersists;

        public string ObjectId { get { return objectId ?? string.Empty; } }

        public int Variation { get { return variation < 0 ? 0 : variation; } }

        /// <summary>
        /// The cap on how many exist at once, never below one.
        /// </summary>
        /// <remarks>
        /// Clamped rather than warned about. A nest with a cap of zero produces without limit until
        /// the world cannot hold any more, and there is no reading of "zero" that a person meant.
        /// </remarks>
        public int AtMostAtOnce { get { return atMostAtOnce < 1 ? 1 : atMostAtOnce; } }

        public float MinWait { get { return minWait < 0f ? 0f : minWait; } }

        public float MaxWait
        {
            get
            {
                float longest = maxWait < 0f ? 0f : maxWait;
                return longest < MinWait ? MinWait : longest;
            }
        }

        public float AppearsWithin { get { return appearsWithin < 0f ? 0f : appearsWithin; } }

        public float MinWaitWhenFull
        {
            get { return minWaitWhenFull < 0f ? 0f : minWaitWhenFull; }
        }

        public float MaxWaitWhenFull
        {
            get
            {
                float longest = maxWaitWhenFull < 0f ? 0f : maxWaitWhenFull;
                return longest < MinWaitWhenFull ? MinWaitWhenFull : longest;
            }
        }

        public bool OnlyWhileFighting { get { return onlyWhileFighting; } }

        public bool AppearsNearPlayers { get { return appearsNearPlayers; } }

        public string[] OnlyInBiomes { get { return onlyInBiomes ?? new string[0]; } }

        public bool PlayerMustBeInThatBiome { get { return playerMustBeInThatBiome; } }

        public string OnlyInSeason { get { return onlyInSeason ?? string.Empty; } }

        public string RequiresCondition { get { return requiresCondition ?? string.Empty; } }

        public string KeepsAwayFrom { get { return keepsAwayFrom ?? string.Empty; } }

        public bool ProducesCritters { get { return producesCritters; } }

        public float CritterDespawnDistance
        {
            get { return critterDespawnDistance < 0f ? 0f : critterDespawnDistance; }
        }

        public bool WhatItMakesPersists { get { return whatItMakesPersists; } }

        /// <summary>Whether the player-must-be-here rule was set with no biomes to be in.</summary>
        public bool BiomeRuleIsIncomplete
        {
            get { return playerMustBeInThatBiome && OnlyInBiomes.Length == 0; }
        }
    }
}
