using System.Collections.Generic;
using Unity.Mathematics;

namespace ExpandNullforge.Zones
{
    /// <summary>What sets a triggered tile off.</summary>
    public enum DimensionTileTrigger
    {
        /// <summary>A player stands on it.</summary>
        PlayerSteps = 0,

        /// <summary>A player stands on it while carrying something.</summary>
        PlayerStepsCarrying = 1,

        /// <summary>Anything that can take damage stands on it — players, creatures, both.</summary>
        AnythingSteps = 2
    }

    /// <summary>What a triggered tile does.</summary>
    /// <remarks>
    /// The same vocabulary boss phases use, and for the same reason: every action is something Core
    /// Keeper already performs, so a trap reads like part of the game rather than a script firing.
    /// </remarks>
    public enum DimensionTileAction
    {
        /// <summary>Nothing — a pure detector, for a tile whose point is the message.</summary>
        None = 0,

        /// <summary>Spawn creatures around the tile.</summary>
        SummonCreatures = 1,

        /// <summary>Apply one of the game's conditions to whoever set it off.</summary>
        ApplyCondition = 2,

        /// <summary>Damage whoever set it off.</summary>
        Damage = 3
    }

    /// <summary>One tile that does something when stepped on.</summary>
    public sealed class DimensionTriggeredTileDefinition
    {
        public DimensionTriggeredTileDefinition(
            string triggerId,
            string dimensionId,
            int2 worldPosition,
            DimensionTileTrigger trigger,
            string triggerTarget,
            DimensionTileAction action,
            string actionTarget,
            int actionAmount,
            float actionDuration,
            float radius,
            bool onceOnly,
            float cooldownSeconds)
        {
            TriggerId = triggerId ?? string.Empty;
            DimensionId = dimensionId ?? string.Empty;
            WorldPosition = worldPosition;
            Trigger = trigger;
            TriggerTarget = triggerTarget ?? string.Empty;
            Action = action;
            ActionTarget = actionTarget ?? string.Empty;
            ActionAmount = actionAmount;
            ActionDuration = actionDuration;
            Radius = radius;
            OnceOnly = onceOnly;
            CooldownSeconds = cooldownSeconds;
        }

        public readonly string TriggerId;
        public readonly string DimensionId;

        /// <summary>
        /// Where it is, in world tile coordinates.
        /// </summary>
        /// <remarks>
        /// World, not dimension-local, because the system that fires triggers finds an entity's cell
        /// by rounding its transform position — which is a world position. The scene placement pass
        /// converts its dimension-local anchor to world coordinates before registering, so the two
        /// sides meet on the same grid.
        /// </remarks>
        public readonly int2 WorldPosition;

        public readonly DimensionTileTrigger Trigger;

        /// <summary>The item a <c>PlayerStepsCarrying</c> trigger looks for.</summary>
        public readonly string TriggerTarget;

        public readonly DimensionTileAction Action;

        /// <summary>The creature to summon or the condition to apply.</summary>
        public readonly string ActionTarget;

        /// <summary>Creatures summoned, condition stacks, or damage dealt.</summary>
        public readonly int ActionAmount;

        public readonly float ActionDuration;
        public readonly float Radius;

        /// <summary>Whether it fires once and is done, like a one-shot trap.</summary>
        public readonly bool OnceOnly;

        /// <summary>
        /// How long before it can fire again.
        /// </summary>
        /// <remarks>
        /// Necessary, not optional: a tile with no cooldown fires every simulation tick a player
        /// stands on it, which turns "a trap" into "instant death and a wall of notifications".
        /// </remarks>
        public readonly float CooldownSeconds;
    }

    /// <summary>
    /// Every tile in this mod that reacts to being stepped on.
    /// </summary>
    /// <remarks>
    /// Core Keeper has traps, but each is an object with its own hardcoded behaviour rather than a
    /// mechanism to configure — so this is structure the framework supplies. The actions are not:
    /// summoning, conditions and damage are all the game's own.
    /// </remarks>
    public static class DimensionTriggeredTileRegistry
    {
        private static readonly Dictionary<long, List<DimensionTriggeredTileDefinition>> ByCell =
            new Dictionary<long, List<DimensionTriggeredTileDefinition>>();

        public static bool HasAny
        {
            get { return ByCell.Count > 0; }
        }

        /// <summary>
        /// A cell's lookup key.
        /// </summary>
        /// <remarks>
        /// Packed into a long so the per-step check is one dictionary probe on a value type. Keying on
        /// the position rather than walking a list is what keeps the cost of this feature proportional
        /// to how many tiles are triggered, not to how often anyone walks anywhere.
        /// </remarks>
        public static long KeyFor(int2 worldPosition)
        {
            return ((long)worldPosition.x << 32) ^ (uint)worldPosition.y;
        }

        public static void Register(DimensionTriggeredTileDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.TriggerId))
            {
                return;
            }

            long key = KeyFor(definition.WorldPosition);
            List<DimensionTriggeredTileDefinition> atCell;
            if (!ByCell.TryGetValue(key, out atCell))
            {
                atCell = new List<DimensionTriggeredTileDefinition>();
                ByCell.Add(key, atCell);
            }

            for (int i = 0; i < atCell.Count; i++)
            {
                if (string.Equals(atCell[i].TriggerId, definition.TriggerId, System.StringComparison.Ordinal))
                {
                    atCell[i] = definition;
                    return;
                }
            }

            atCell.Add(definition);
        }

        /// <summary>Whatever is set up on a cell, or null when nothing is.</summary>
        public static IReadOnlyList<DimensionTriggeredTileDefinition> GetAt(int2 worldPosition)
        {
            List<DimensionTriggeredTileDefinition> atCell;
            return ByCell.TryGetValue(KeyFor(worldPosition), out atCell) ? atCell : null;
        }

        public static void Clear()
        {
            ByCell.Clear();
        }
    }
}
