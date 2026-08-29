using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>One condition a thing starts the world with.</summary>
    /// <remarks>
    /// The same shape as Core Keeper's <c>ConditionData</c>: which condition, how long it lasts, how
    /// strong it is, and a multiplier the level curve feeds. A duration of 0 is not "no time" — it
    /// is how vanilla says "for as long as this thing exists", which is what makes a permanently
    /// enraged creature possible.
    /// </remarks>
    [Serializable]
    public sealed class DimensionStartingCondition
    {
        [Tooltip("Which condition. A ConditionID name from the game's own list.")]
        [SerializeField] private string conditionId = string.Empty;

        [Tooltip("How long it lasts, in seconds. 0 means for as long as the thing exists.")]
        [Min(0f)]
        [SerializeField] private float seconds;

        [Tooltip("How strong it is.")]
        [SerializeField] private int strength = 1;

        [Tooltip("Multiplier the level curve applies to the strength.")]
        [Min(0f)]
        [SerializeField] private float strengthMultiplier = 1f;

        [Tooltip("How often it happens at all, 0 to 1. 1 means always.")]
        [Range(0f, 1f)]
        [SerializeField] private float chance = 1f;

        public string ConditionId
        {
            get { return conditionId ?? string.Empty; }
        }

        public float Seconds
        {
            get { return seconds < 0f ? 0f : seconds; }
        }

        public int Strength
        {
            get { return strength; }
        }

        public float StrengthMultiplier
        {
            get { return strengthMultiplier < 0f ? 0f : strengthMultiplier; }
        }

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

        public bool NamesACondition
        {
            get { return !string.IsNullOrEmpty(ConditionId); }
        }

        /// <summary>Whether it is certain rather than a roll.</summary>
        /// <remarks>
        /// Core Keeper keeps the two apart: a certain condition goes in <c>initialConditions</c> and
        /// a chanced one in <c>initialConditionsWithRandomChance</c>. Asking a creator for one field
        /// and sorting them here is the whole point of having a framework.
        /// </remarks>
        public bool IsCertain
        {
            get { return Chance >= 1f; }
        }

        /// <summary>Whether it names a condition and can never happen.</summary>
        public bool CanNeverHappen
        {
            get { return NamesACondition && Chance <= 0f; }
        }
    }

    /// <summary>
    /// What a thing starts out affected by, and what it shrugs off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>SupportsConditionsAuthoring</c> is on almost everything the framework generates and,
    /// until now, <b>nothing on it was ever set</b>. Its presence made every generated object look
    /// configured for conditions while none of the six authorable fields had been touched.
    /// </para>
    /// <para>
    /// The load-bearing one is <c>initialConditions</c>: a creature that starts enraged, a plant
    /// that starts blighted, an item that comes already blessed. Without it, the only way to give
    /// something a condition is to hit it with one.
    /// </para>
    /// <para>
    /// The three immunities are the other half of the same question — a construct that no aura
    /// reaches, a thing the environment cannot chill, something healing does not touch.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionInitialConditionsTemplate
    {
        [Tooltip("What it starts the world already affected by.")]
        [SerializeField] private DimensionStartingCondition[] startsWith = new DimensionStartingCondition[0];

        [Header("What conditions cannot touch it")]
        [Tooltip("No aura reaches it.")]
        [SerializeField] private bool aurasDoNotReachIt;

        [Tooltip("The environment cannot chill, burn or poison it.")]
        [SerializeField] private bool theEnvironmentDoesNotAffectIt;

        [Tooltip("Healing does not touch it.")]
        [SerializeField] private bool healingDoesNotTouchIt;

        [Tooltip("Its stats are not listed on the item tooltip.")]
        [SerializeField] private bool hideStatsOnTheTooltip;

        /// <summary>Everything it starts with, blanks dropped.</summary>
        public DimensionStartingCondition[] StartsWith
        {
            get
            {
                DimensionStartingCondition[] all = startsWith ?? new DimensionStartingCondition[0];
                System.Collections.Generic.List<DimensionStartingCondition> kept =
                    new System.Collections.Generic.List<DimensionStartingCondition>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].NamesACondition)
                    {
                        kept.Add(all[i]);
                    }
                }

                return kept.ToArray();
            }
        }

        public bool StartsWithAnything
        {
            get { return StartsWith.Length > 0; }
        }

        public bool AurasDoNotReachIt
        {
            get { return aurasDoNotReachIt; }
        }

        public bool TheEnvironmentDoesNotAffectIt
        {
            get { return theEnvironmentDoesNotAffectIt; }
        }

        public bool HealingDoesNotTouchIt
        {
            get { return healingDoesNotTouchIt; }
        }

        public bool HideStatsOnTheTooltip
        {
            get { return hideStatsOnTheTooltip; }
        }

        /// <summary>Whether anything here is set at all.</summary>
        public bool SaysAnything
        {
            get
            {
                return StartsWithAnything
                    || aurasDoNotReachIt
                    || theEnvironmentDoesNotAffectIt
                    || healingDoesNotTouchIt
                    || hideStatsOnTheTooltip;
            }
        }

        /// <summary>
        /// Whether it starts healed-immune and starts with a healing condition, which cancel out.
        /// </summary>
        /// <remarks>
        /// Not an error the game reports — the condition is applied and then does nothing, which
        /// reads as the condition being broken rather than as two settings disagreeing.
        /// </remarks>
        public bool ImmuneToSomethingItStartsWith
        {
            get
            {
                if (!healingDoesNotTouchIt)
                {
                    return false;
                }

                DimensionStartingCondition[] all = StartsWith;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].ConditionId.IndexOf("heal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
