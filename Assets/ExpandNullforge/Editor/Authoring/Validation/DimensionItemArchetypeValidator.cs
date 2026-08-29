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

            // Stacking used to be checked here, back when it was a number that could be zero.
            // It is a yes/no now — the only shape Core Keeper's ObjectInfo has — and neither
            // answer is invalid, so there is nothing left to check.

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

            // Durability is validated on the MULTIPLIER, not on the points.
            //
            // This used to be the other way round, and it was a contradiction: the validator refused
            // to generate equipment without a durability number, and the generator then told the
            // author that Core Keeper recomputes durability from the item type and their number was
            // discarded. Equipment could not be made at all without filling in a field that did
            // nothing.
            //
            // The multiplier is what the game multiplies its own type-based base by, so a zero there
            // is the real failure: equipment that breaks the moment it is used.
            if (Requires(required, DimensionItemAuthoringComponents.Durability) &&
                item.DurabilityMultiplier <= 0f)
            {
                findings.Add(new Finding(Severity.Error, "durabilityMultiplier",
                    "Equipment with a durability multiplier of zero has no durability at all, so "
                    + "it breaks the moment it is used."));
            }

            if (Requires(required, DimensionItemAuthoringComponents.WeaponDamage) &&
                item.DamageAmount <= 0)
            {
                findings.Add(new Finding(Severity.Error, "damageAmount",
                    "A weapon needs damage above zero."));
            }

            // True of the object on disk, not a hope about the runtime. The generator writes the
            // game's own number — 0.4 seconds, or 0.6 for a bow, a thrown weapon, or a summoning weapon that summons a minion on right-click — into the
            // cooldown component when this is blank, because a component holding zero is read as no
            // delay at all rather than as "nothing was set".
            if (Requires(required, DimensionItemAuthoringComponents.Cooldown) &&
                item.CooldownSeconds <= 0f)
            {
                findings.Add(new Finding(Severity.Warning, "cooldownSeconds",
                    "No time between uses set; the generated object is given the game's own "
                    + "default, which is 0.4 seconds, or 0.6 for a bow, a thrown weapon, or a summoning weapon that summons a minion on right-click."));
            }

            // A bomb that does not explode is not a bomb. The archetype's whole reason to exist is
            // the answers under "if it goes off", and every one of them is ignored while the tick
            // is off — so an author who chose Bomb and left it off would get an ordinary placeable
            // and no explanation.
            if (Requires(required, DimensionItemAuthoringComponents.Explosive) &&
                !item.Explosive.Explodes)
            {
                findings.Add(new Finding(Severity.Error, "explosive",
                    "A bomb has to explode. Tick 'it explodes' under 'If it goes off', or change " +
                    "the archetype to Placeable object."));
            }

            if (item.Explosive.HasAFuseThatNeverBurnsDown)
            {
                findings.Add(new Finding(Severity.Error, "explosive",
                    "Its countdown is zero seconds, which never runs out, so it can only ever be " +
                    "set off by being broken."));
            }

            if (item.Explosive.WaitsForSomethingThatCannotArrive)
            {
                findings.Add(new Finding(Severity.Error, "explosive",
                    "It goes off when something comes within zero tiles, which nothing ever can. " +
                    "Vanilla's proximity bomb uses 1."));
            }

            if (item.Explosive.ExplodesAndReachesNothing)
            {
                findings.Add(new Finding(Severity.Error, "explosive",
                    "Its blast reaches nothing, so it goes off and catches nobody. Vanilla's " +
                    "ordinary bomb reaches " +
                    DimensionExplosiveTemplate.OrdinaryBlastReach + " tiles."));
            }

            if (item.Explosive.ExplodesHarmlessly)
            {
                findings.Add(new Finding(Severity.Warning, "explosive",
                    "It explodes and does no damage of any kind when it does."));
            }

            if (item.Explosive.DigsLessFarThanItReaches)
            {
                findings.Add(new Finding(Severity.Warning, "explosive",
                    "It reaches further than terrain damage can travel. The game only breaks " +
                    "terrain within " + DimensionExplosiveTemplate.TerrainReachCap +
                    " tiles of a blast, so the extra reach catches creatures and nothing else."));
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
