using System.Collections.Generic;

namespace ExpandNullforge.Zones
{
    /// <summary>
    /// What a boss does when it crosses a health threshold.
    /// </summary>
    /// <remarks>
    /// Every action here is a thing Core Keeper already does to a creature — summon something, apply
    /// a condition, restore health. Nothing invents a new effect, so a phase reads and behaves like
    /// the rest of the game rather than like a script running on top of it.
    /// </remarks>
    public enum DimensionBossPhaseAction
    {
        /// <summary>Nothing but the phase change itself, for a purely announced beat.</summary>
        None = 0,

        /// <summary>Spawn creatures around the boss.</summary>
        SummonAdds = 1,

        /// <summary>Apply one of the game's conditions to the boss.</summary>
        ApplyConditionToSelf = 2,

        /// <summary>Apply one of the game's conditions to everyone fighting it.</summary>
        ApplyConditionToPlayers = 3,

        /// <summary>Restore some of the boss's health.</summary>
        Heal = 4
    }

    /// <summary>One threshold and what happens when the boss falls past it.</summary>
    public sealed class DimensionBossPhaseDefinition
    {
        public DimensionBossPhaseDefinition(
            string phaseId,
            string bossObjectName,
            float healthThreshold,
            DimensionBossPhaseAction action,
            string actionTarget,
            int actionAmount,
            float actionDuration,
            float radius,
            string musicCueId = "")
        {
            PhaseId = phaseId ?? string.Empty;
            BossObjectName = bossObjectName ?? string.Empty;
            HealthThreshold = healthThreshold;
            Action = action;
            ActionTarget = actionTarget ?? string.Empty;
            ActionAmount = actionAmount;
            ActionDuration = actionDuration;
            Radius = radius;
            MusicCueId = musicCueId ?? string.Empty;
        }

        public readonly string PhaseId;
        public readonly string BossObjectName;

        /// <summary>The fraction of health at or below which this phase begins.</summary>
        public readonly float HealthThreshold;

        public readonly DimensionBossPhaseAction Action;

        /// <summary>What the action acts with — a creature to summon, or a condition to apply.</summary>
        public readonly string ActionTarget;

        /// <summary>How many: creatures summoned, condition stacks, or health restored.</summary>
        public readonly int ActionAmount;

        /// <summary>How long a condition lasts, in seconds. Zero uses the condition's own duration.</summary>
        public readonly float ActionDuration;

        /// <summary>How far from the boss the action reaches.</summary>
        public readonly float Radius;

        /// <summary>Music that takes over when this phase begins; empty keeps the current cue.</summary>
        public readonly string MusicCueId;
    }

    /// <summary>
    /// Every boss phase this mod defines, in threshold order per boss.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core Keeper's own bosses have phases, but each one is its own hardcoded state machine — there
    /// is no reusable phase mechanism to borrow. This is therefore one of the few places the framework
    /// genuinely has to supply the structure rather than reuse it. What it does NOT supply is the
    /// effects: every action a phase can take is one the game already performs.
    /// </para>
    /// <para>
    /// Phases are kept sorted from highest threshold to lowest, so a boss dropped straight past
    /// several thresholds by one big hit fires them in the order the author wrote them rather than in
    /// registration order.
    /// </para>
    /// </remarks>
    public static class DimensionBossPhaseRegistry
    {
        private static readonly Dictionary<string, List<DimensionBossPhaseDefinition>> ByBoss =
            new Dictionary<string, List<DimensionBossPhaseDefinition>>(System.StringComparer.Ordinal);

        public static bool HasAny
        {
            get { return ByBoss.Count > 0; }
        }

        public static void Register(
            string phaseId,
            string bossObjectName,
            float healthThreshold,
            DimensionBossPhaseAction action,
            string actionTarget,
            int actionAmount,
            float actionDuration,
            float radius,
            string musicCueId = "")
        {
            if (string.IsNullOrEmpty(bossObjectName))
            {
                return;
            }

            List<DimensionBossPhaseDefinition> phases;
            if (!ByBoss.TryGetValue(bossObjectName, out phases))
            {
                phases = new List<DimensionBossPhaseDefinition>();
                ByBoss.Add(bossObjectName, phases);
            }

            DimensionBossPhaseDefinition definition = new DimensionBossPhaseDefinition(
                phaseId,
                bossObjectName,
                healthThreshold,
                action,
                actionTarget,
                actionAmount,
                actionDuration,
                radius,
                musicCueId);

            for (int i = 0; i < phases.Count; i++)
            {
                if (string.Equals(phases[i].PhaseId, definition.PhaseId, System.StringComparison.Ordinal))
                {
                    phases[i] = definition;
                    Sort(phases);
                    return;
                }
            }

            phases.Add(definition);
            Sort(phases);
        }

        /// <summary>Every boss that has phases, so the runtime can resolve their ids once.</summary>
        public static IReadOnlyList<string> BossNames
        {
            get
            {
                List<string> names = new List<string>(ByBoss.Count);
                foreach (KeyValuePair<string, List<DimensionBossPhaseDefinition>> pair in ByBoss)
                {
                    names.Add(pair.Key);
                }

                return names;
            }
        }

        /// <summary>The phases for a boss, highest threshold first, or null if it has none.</summary>
        public static IReadOnlyList<DimensionBossPhaseDefinition> GetPhases(string bossObjectName)
        {
            List<DimensionBossPhaseDefinition> phases;
            return ByBoss.TryGetValue(bossObjectName ?? string.Empty, out phases) ? phases : null;
        }

        public static void Clear()
        {
            ByBoss.Clear();
        }

        private static void Sort(List<DimensionBossPhaseDefinition> phases)
        {
            phases.Sort(delegate (DimensionBossPhaseDefinition a, DimensionBossPhaseDefinition b)
            {
                return b.HealthThreshold.CompareTo(a.HealthThreshold);
            });
        }
    }
}
