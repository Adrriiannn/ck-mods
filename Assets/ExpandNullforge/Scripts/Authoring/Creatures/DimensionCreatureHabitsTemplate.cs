using System;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The habits that make a creature feel like it lives somewhere: how it loiters, how long it
    /// stays angry, whether it keeps a home, whether it can be kept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Creatures already move and fight well. What they do not yet do is <i>behave</i> — react when
    /// a player wanders close, taunt mid-fight, stay riled near their nest, claim a bed, get hungry.
    /// These are eleven small vanilla components, none of them more than a couple of settings, and
    /// together they are most of the difference between a monster and an inhabitant.
    /// </para>
    /// <para>
    /// MEASURED: <c>IdleWhenNearbyPlayerState</c> 54 prefabs, <c>IdleInCombatState</c> 39,
    /// <c>HasSpawnPoint</c> 39, <c>Cattle</c> 36, <c>PutTargetInCombatOnDealingDamage</c> 36,
    /// <c>Fullness</c> 33, <c>IsFlying</c> 24, <c>CanClaimBed</c> 21, <c>MealsEaten</c> 18,
    /// <c>CombatEmoteState</c> 18, <c>OverrideLeaveCombatTime</c> 24.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionCreatureHabitsTemplate
    {
        [Header("Loitering")]
        [Tooltip("It stops and takes notice when a player comes within this far. 0 for never.")]
        [Min(0f)]
        [SerializeField] private float noticesAPlayerWithin;

        [Tooltip("It pauses mid-fight rather than pressing constantly. Distance it backs off to.")]
        [Min(0f)]
        [SerializeField] private float pausesInCombatBeyond;

        [Tooltip("It measures that distance from its nest rather than from itself.")]
        [SerializeField] private bool measuresFromItsNest;

        [Header("Taunting")]
        [Tooltip("It plays taunts and emotes during a fight.")]
        [SerializeField] private bool tauntsDuringAFight;

        [Tooltip("Chance it taunts the instant a fight starts, rather than waiting.")]
        [Range(0f, 1f)]
        [SerializeField] private float tauntsImmediatelyChance;

        [Tooltip("Shortest wait between taunts.")]
        [Min(0f)]
        [SerializeField] private float minBetweenTaunts = 4f;

        [Tooltip("Longest wait between taunts.")]
        [Min(0f)]
        [SerializeField] private float maxBetweenTaunts = 6f;

        [Tooltip("The taunt animations it plays, by the game's own animation names.")]
        [SerializeField] private DimensionTaunt[] taunts = new DimensionTaunt[0];

        [Header("Staying angry")]
        [Tooltip("How long it stays in a fight after losing sight. 0 uses the game's own time.")]
        [Min(0f)]
        [SerializeField] private float staysAngryFor;

        [Tooltip("It refuses to calm down while a player is near its nest.")]
        [SerializeField] private bool guardsItsNest;

        [Tooltip("How close to the nest keeps it riled. The game's own value is 10.")]
        [Min(0f)]
        [SerializeField] private float guardsWithin = 10f;

        [Tooltip("Anything it hurts is dragged into the fight too.")]
        [SerializeField] private bool draggingWhatItHurtsIntoTheFight;

        [Header("Home and keeping")]
        [Tooltip("It remembers where it spawned and treats that as home.")]
        [SerializeField] private bool keepsANest;

        [Tooltip("It flies, so the ground beneath it does not slow it.")]
        [SerializeField] private bool flies;

        [Tooltip("It can claim a bed and sleep in it.")]
        [SerializeField] private bool canClaimABed;

        [Tooltip("It is livestock — it can be kept, fed and farmed.")]
        [SerializeField] private bool isLivestock;

        [Tooltip("It gets full, so it can only be fed so much. 0 for never full.")]
        [Min(0)]
        [SerializeField] private int getsFullAt;

        [Tooltip("It remembers how many meals it has eaten — what breeding and growing count from.")]
        [SerializeField] private bool remembersItsMeals;

        [Tooltip("It can be commanded to move to a spot, the way a minion is.")]
        [SerializeField] private bool takesMoveOrders;

        [Header("On death")]
        [Tooltip("Killing it unlocks an achievement, by the game's own name. Blank for none.")]
        [SerializeField] private string unlocksAchievement = string.Empty;

        public float NoticesAPlayerWithin
        {
            get { return noticesAPlayerWithin < 0f ? 0f : noticesAPlayerWithin; }
        }

        public float PausesInCombatBeyond
        {
            get { return pausesInCombatBeyond < 0f ? 0f : pausesInCombatBeyond; }
        }

        public bool MeasuresFromItsNest { get { return measuresFromItsNest; } }

        public bool TauntsDuringAFight { get { return tauntsDuringAFight; } }

        public float TauntsImmediatelyChance
        {
            get
            {
                if (tauntsImmediatelyChance < 0f)
                {
                    return 0f;
                }

                return tauntsImmediatelyChance > 1f ? 1f : tauntsImmediatelyChance;
            }
        }

        public float MinBetweenTaunts
        {
            get { return minBetweenTaunts < 0f ? 0f : minBetweenTaunts; }
        }

        public float MaxBetweenTaunts
        {
            get
            {
                float longest = maxBetweenTaunts < 0f ? 0f : maxBetweenTaunts;
                return longest < MinBetweenTaunts ? MinBetweenTaunts : longest;
            }
        }

        public DimensionTaunt[] Taunts { get { return taunts ?? new DimensionTaunt[0]; } }

        public float StaysAngryFor { get { return staysAngryFor < 0f ? 0f : staysAngryFor; } }

        public bool GuardsItsNest { get { return guardsItsNest; } }

        public float GuardsWithin { get { return guardsWithin < 0f ? 0f : guardsWithin; } }

        public bool DraggingWhatItHurtsIntoTheFight
        {
            get { return draggingWhatItHurtsIntoTheFight; }
        }

        /// <summary>Whether it keeps a nest, which guarding also requires.</summary>
        public bool KeepsANest { get { return keepsANest || guardsItsNest; } }

        public bool Flies { get { return flies; } }

        public bool CanClaimABed { get { return canClaimABed; } }

        public bool IsLivestock { get { return isLivestock; } }

        public int GetsFullAt { get { return getsFullAt < 0 ? 0 : getsFullAt; } }

        public bool RemembersItsMeals { get { return remembersItsMeals; } }

        public bool TakesMoveOrders { get { return takesMoveOrders; } }

        public string UnlocksAchievement { get { return unlocksAchievement ?? string.Empty; } }

        /// <summary>Whether it taunts with nothing to taunt with.</summary>
        public bool TauntsWithNoAnimations
        {
            get { return tauntsDuringAFight && Taunts.Length == 0; }
        }

        /// <summary>
        /// Whether it guards a nest it does not keep.
        /// </summary>
        /// <remarks>
        /// Not an error — asking it to guard implies keeping one, so the nest is added for it. Said
        /// out loud because otherwise the two tickboxes look independent and one silently turns the
        /// other on.
        /// </remarks>
        public bool GuardsWithoutAskingForANest
        {
            get { return guardsItsNest && !keepsANest; }
        }
    }

    /// <summary>One taunt a creature plays mid-fight.</summary>
    [Serializable]
    public struct DimensionTaunt
    {
        [Tooltip("The animation it plays, by the game's own animation name.")]
        [SerializeField] private string animation;

        [Tooltip("How long it lasts.")]
        [Min(0f)]
        [SerializeField] private float seconds;

        [Tooltip("Shortest it holds this before a fight actually starts.")]
        [Min(0f)]
        [SerializeField] private float minBeforeCombat;

        [Tooltip("Longest it holds this before a fight actually starts.")]
        [Min(0f)]
        [SerializeField] private float maxBeforeCombat;

        public string Animation { get { return animation ?? string.Empty; } }

        public float Seconds { get { return seconds < 0f ? 0f : seconds; } }

        public float MinBeforeCombat
        {
            get { return minBeforeCombat < 0f ? 0f : minBeforeCombat; }
        }

        public float MaxBeforeCombat
        {
            get
            {
                float longest = maxBeforeCombat < 0f ? 0f : maxBeforeCombat;
                return longest < MinBeforeCombat ? MinBeforeCombat : longest;
            }
        }
    }
}
