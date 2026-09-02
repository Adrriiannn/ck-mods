using System;
using ExpandNullforge.Zones;
using UnityEngine;

namespace ExpandNullforge.Authoring
{
    // Scene furniture travels through DimensionSceneObjectTemplate — placed objects, chest
    // contents and fixed creatures alike — and ambient population is the per-creature spawn chain.
    // There are deliberately no separate prop, loot-container or spawn-point templates beside this
    // class: those were the pre-CustomSceneBlob plan and would be settings that did nothing.

    /// <summary>
    /// A patch of a scene's floor that does something when stepped on — a trap, a pressure plate,
    /// a welcome mat that summons the welcoming party.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This speaks the vocabulary of <see cref="DimensionTriggeredTileRegistry"/> because that is
    /// what fires it: the bootstrap carries these fields to the scene registration, and the scene
    /// placement pass arms every covered tile at the spot the scene actually lands. An earlier
    /// shape of this template named an event bus that never existed; every field here is read by
    /// the running trigger system.
    /// </para>
    /// <para>
    /// KNOWN LIMIT. Triggers arm only where the framework's own scene pass places the scene and so
    /// knows where it landed. A scene stamped into a generated dungeon room goes through Core
    /// Keeper's own machinery, which reports no position — its triggers stay silent.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class DimensionSceneTriggerTemplate
    {
        [SerializeField] private string triggerId = "trigger";
        [SerializeField] private string displayName = "Trigger";

        [Tooltip("The covered patch, in the scene's own tile coordinates — the same grid the tiles use.")]
        [SerializeField] private Vector2Int localMin;
        [SerializeField] private Vector2Int localMaxExclusive = new Vector2Int(1, 1);

        [Tooltip("What sets it off: a player stepping on it, a player carrying a particular item, or anything at all.")]
        [SerializeField] private DimensionTileTrigger kind = DimensionTileTrigger.PlayerSteps;

        [Tooltip("The item a carrying trigger looks for. Only read when the kind above is the carrying one.")]
        [SerializeField] private string carriedItemId = string.Empty;

        [Tooltip("What happens: summon creatures, apply one of the game's conditions, or deal damage.")]
        [SerializeField] private DimensionTileAction action = DimensionTileAction.Damage;

        [Tooltip("The creature to summon or the condition to apply, by name. Damage needs no target.")]
        [SerializeField] private string target = string.Empty;

        [Tooltip("Creatures summoned, condition stacks, or damage dealt.")]
        [Min(1)]
        [SerializeField] private int amount = 1;

        [Tooltip("How long an applied condition lasts, in seconds. Only read by the condition action.")]
        [Min(0f)]
        [SerializeField] private float conditionSeconds;

        [Tooltip("How long before it can fire again, in seconds.")]
        [Min(0f)]
        [SerializeField] private float cooldownSeconds = 1f;

        [Tooltip("It fires once and is done, like a one-shot trap.")]
        [SerializeField] private bool onceOnly;

        [SerializeField] private bool enabled = true;
        [SerializeField] private string notes = string.Empty;

        public string TriggerId { get { return triggerId ?? string.Empty; } }
        public string DisplayName { get { return displayName ?? string.Empty; } }
        public Vector2Int LocalMin { get { return localMin; } }
        public Vector2Int LocalMaxExclusive { get { return EnsureExclusiveMax(localMin, localMaxExclusive); } }
        public DimensionTileTrigger Kind { get { return kind; } }
        public string CarriedItemId { get { return carriedItemId ?? string.Empty; } }
        public DimensionTileAction Action { get { return action; } }
        public string Target { get { return target ?? string.Empty; } }
        public int Amount { get { return amount < 1 ? 1 : amount; } }
        public float ConditionSeconds { get { return conditionSeconds < 0f ? 0f : conditionSeconds; } }
        public float CooldownSeconds { get { return cooldownSeconds < 0f ? 0f : cooldownSeconds; } }
        public bool OnceOnly { get { return onceOnly; } }
        public bool Enabled { get { return enabled; } }
        public string Notes { get { return notes ?? string.Empty; } }

        private static Vector2Int EnsureExclusiveMax(Vector2Int min, Vector2Int maxExclusive)
        {
            return new Vector2Int(
                Mathf.Max(min.x + 1, maxExclusive.x),
                Mathf.Max(min.y + 1, maxExclusive.y));
        }
    }
}
