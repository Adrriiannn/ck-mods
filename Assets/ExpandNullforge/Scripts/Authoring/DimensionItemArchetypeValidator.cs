using System.Collections.Generic;
using ExpandNullforge.Api;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// Validates a custom item against the components its archetype requires, so a creator is
    /// told exactly what is still missing before the object is generated — rather than
    /// discovering an incomplete object in-game. Pure logic: no AssetDatabase, no runtime.
    /// </summary>
    public static class DimensionItemArchetypeValidator
    {
        public enum Severity
        {
            Ok,
            Warning,
            Error
        }

        public readonly struct Finding
        {
            public Finding(Severity severity, string field, string message)
            {
                Severity = severity;
                Field = field ?? string.Empty;
                Message = message ?? string.Empty;
            }

            public Severity Severity { get; }

            /// <summary>Authoring field the creator should fix, for a click-to-locate action.</summary>
            public string Field { get; }

            public string Message { get; }
        }

        /// <summary>
        /// Returns one finding per unmet requirement. An empty list means the item has every
        /// value its archetype needs. Disabled items are reported as a single informational
        /// finding so they are never silently skipped.
        /// </summary>
        public static List<Finding> Validate(DimensionItemAsset item)
        {
            List<Finding> findings = new List<Finding>();
            if (item == null)
            {
                findings.Add(new Finding(Severity.Error, string.Empty, "No item asset supplied."));
                return findings;
            }

            DimensionItemArchetype archetype = item.Archetype;
            DimensionItemAuthoringComponents required =
                DimensionItemArchetypeRules.GetRequiredComponents(archetype);

            // Always-required basics.
            if (string.IsNullOrEmpty(item.ItemId))
            {
                findings.Add(new Finding(Severity.Error, "itemId",
                    "An item id is required; it becomes the Core Keeper ObjectID."));
            }

            if (string.IsNullOrEmpty(item.DisplayName))
            {
                findings.Add(new Finding(Severity.Error, "displayName",
                    "A display name is required for the generated localization entry."));
            }

            if (Requires(required, DimensionItemAuthoringComponents.Visual) && !item.HasVisual)
            {
                findings.Add(new Finding(Severity.Error, "iconSprite",
                    "A " + DimensionItemArchetypeRules.Describe(archetype) +
                    " needs a sprite to be visible. Drag one into Icon sprite."));
            }

            if (Requires(required, DimensionItemAuthoringComponents.InventoryItem) &&
                item.MaxStack <= 0)
            {
                findings.Add(new Finding(Severity.Error, "maxStack",
                    "An inventory item needs a maximum stack size of at least 1."));
            }

            if (Requires(required, DimensionItemAuthoringComponents.Loot) &&
                string.IsNullOrEmpty(item.LootTableId))
            {
                findings.Add(new Finding(Severity.Error, "lootTableId",
                    "A " + DimensionItemArchetypeRules.Describe(archetype) +
                    " drops loot, so it needs a loot table."));
            }

            if (Requires(required, DimensionItemAuthoringComponents.Breakable) &&
                item.HealthPoints <= 0)
            {
                findings.Add(new Finding(Severity.Error, "healthPoints",
                    "A destructible object needs health above zero or it can never be broken."));
            }

            if (Requires(required, DimensionItemAuthoringComponents.Creature) &&
                item.HealthPoints <= 0)
            {
                findings.Add(new Finding(Severity.Error, "healthPoints",
                    "A creature needs health above zero."));
            }

            if (Requires(required, DimensionItemAuthoringComponents.Durability) &&
                item.DurabilityPoints <= 0)
            {
                findings.Add(new Finding(Severity.Error, "durabilityPoints",
                    "Equipment needs durability above zero."));
            }

            if (Requires(required, DimensionItemAuthoringComponents.WeaponDamage) &&
                item.DamageAmount <= 0)
            {
                findings.Add(new Finding(Severity.Error, "damageAmount",
                    "A weapon needs damage above zero."));
            }

            if (Requires(required, DimensionItemAuthoringComponents.Cooldown) &&
                item.CooldownSeconds <= 0f)
            {
                findings.Add(new Finding(Severity.Warning, "cooldownSeconds",
                    "No cooldown set; the generated object will use the vanilla default."));
            }

            if (!item.Enabled)
            {
                findings.Add(new Finding(Severity.Warning, "enabled",
                    "This item is disabled and will not be generated."));
            }

            return findings;
        }

        /// <summary>True when nothing blocks generation (warnings are allowed).</summary>
        public static bool CanGenerate(DimensionItemAsset item)
        {
            List<Finding> findings = Validate(item);
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Severity == Severity.Error)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Requires(
            DimensionItemAuthoringComponents required,
            DimensionItemAuthoringComponents component)
        {
            return (required & component) == component;
        }
    }
}
