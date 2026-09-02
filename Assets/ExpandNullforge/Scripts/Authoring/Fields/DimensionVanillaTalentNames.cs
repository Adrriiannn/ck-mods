// GENERATED FILE — do not hand-edit.
//
// Produced by Docs/harvest-talent-names.sh from the shipped SkillTalentsTable, whose twelve trees
// hold eight talents each. Re-run the script after a Core Keeper update.
using System.Collections.Generic;

namespace ExpandNullforge.Authoring
{
    /// <summary>
    /// The names Core Keeper's own talents go by, which is what tells a rewrite from a new talent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A TALENT'S NAME IS ITS KEY, not its wording. The talent window shows
    /// <c>SkillTalents/&lt;name&gt;</c> looked up in the language table, so writing one of these
    /// names keeps the game's own wording in every language it ships. Writing anything else needs a
    /// line of your own, or the square shows the raw key.
    /// </para>
    /// <para>
    /// The order within a skill is the order the talent window lays the eight squares out in.
    /// </para>
    /// </remarks>
    public static class DimensionVanillaTalentNames
    {
        /// <summary>Whether this is one of the game's own talent names.</summary>
        public static bool IsAGameTalent(string name)
        {
            return !string.IsNullOrEmpty(name) && Known.Contains(name);
        }

        /// <summary>The eight names on one skill's tree, in the order the window shows them.</summary>
        public static IReadOnlyList<string> ForSkill(SkillID skill)
        {
            int index = (int)skill;
            return index >= 0 && index < BySkill.Length ? BySkill[index] : new string[0];
        }

        /// <summary>The eight talents on each of the twelve trees, tree by tree.</summary>
        public static readonly string[][] BySkill = new string[][]
        {
            new string[]
            {
                "MiningDamagePercentage",
                "ChanceForExtraOre",
                "GainDurabilityOnOreWall",
                "MiningDamageAsMeleeDamage",
                "MiningSpeedIncrease",
                "ExplosivesImprovement",
                "MovementIncreaseAfterMining",
                "ChanceForRandomLootFromWall",
            },
            new string[]
            {
                "DrainLessHunger",
                "DodgeIncreaseWhileStandingStill",
                "MovementIncreaseAfterConsistentRunning",
                "MovementIncreaseAfterDodge",
                "SnareTimeReduction",
                "StackingDamageWhileRunning",
                "NearbyEnemiesSlowedDown",
                "ArmorIncreaseFromMovementSpeed",
            },
            new string[]
            {
                "MeleeAttackSpeed",
                "StackingMeleeDamage",
                "IncreaseAttackSpeedOnNextThreeAttacksChance",
                "KnockbackChance",
                "MeleeDamageIncreaseOnHittingSameTarget",
                "ChanceOnMeleeHitToIncreaseRangeDamage",
                "ChanceToRestoreHealthOnMeleeHit",
                "DamageIncreaseAgainstBosses",
            },
            new string[]
            {
                "MaxHealthIncreaseFromAllSkillPoints",
                "DamageIncreaseAtMaxHealth",
                "DamageIncreaseAtLowerHealth",
                "HealthRegenIncreaseBelowHalfHealth",
                "HealingEffectivenessIncrease",
                "HealingPotionAddHealOverTime",
                "ReducedDamageTakenFromBosses",
                "ChanceToNotDieOnLethalHit",
            },
            new string[]
            {
                "ExtraItemChanceAtBaseBuilding",
                "ToolDurabilityLastsLonger",
                "EquipmentDurabilityLastsLonger",
                "ExtraItemChanceAtAlchemyTable",
                "ExtraItemChanceAtRailsWiresAndBelts",
                "CostReductionAtAnvil",
                "PolishedItemChanceAtJewelryTable",
                "IncreasedArmorAtLowHealth",
            },
            new string[]
            {
                "RangeAttackSpeed",
                "StackingRangeDamage",
                "GuaranteedRangeCritsChance",
                "ChanceToApplySlimeOnRangeHit",
                "ChanceOnRangeHitToIncreaseMeleeDamage",
                "ChanceToStunOnRangeHit",
                "StandingStillIncreaseRangeDamage",
                "CriticalDamageMultiplier",
            },
            new string[]
            {
                "SeedFromPlantChanceIncrease",
                "GainMoreFood",
                "GainHungerFromHarvesting",
                "CriticalHitMultiplier",
                "ThornsDamageIncrease",
                "ChanceToApplyPoison",
                "ChanceToGainHigherRarityPlants",
                "DamageIncreaseAgainstPoisoned",
            },
            new string[]
            {
                "FishBitesFaster",
                "FishChanceIncrease",
                "FishStartsCloserToBeReeledIn",
                "DodgeChanceIncrease",
                "ReducedImpactOfSlipperyMovement",
                "ChanceToPreserveBait",
                "FishingAsRangeDamage",
                "IncreaseDamageWhenEatingFish",
            },
            new string[]
            {
                "GainMoreFoodFromCookedFood",
                "WellFedBuffsThresholdReduction",
                "StrongerWellFedBuffs",
                "IncreaseAttackSpeedFromCookedFood",
                "FoodBuffsLastLonger",
                "IncreaseDamageForAllNearbyAllies",
                "ChanceToGainHigherRarityCookedFood",
                "NearbyAlliesGainHealingFromCookedFood",
            },
            new string[]
            {
                "CritChance",
                "ManaOnCrit",
                "MagicBarrierDelayReduction",
                "MagicBarrierAsDamage",
                "BonusDamageOnCrit",
                "MagicBarrierAsManaRegeneration",
                "RemainingManaAsDamage",
                "ManaRegenDelayReduction",
            },
            new string[]
            {
                "MinionAttackSpeed",
                "MinionCritChance",
                "PlayerAttackSpeedPerMinion",
                "MagicDamagaAsMinionDamage",
                "MinionCountAsMagicBarrier",
                "MinionCountAsMagicDamage",
                "MinionLifespan",
                "MinionDespawnHealsPlayer",
            },
            new string[]
            {
                "IncreasedExplosiveRadius",
                "ChanceToNotConsumeExplosives",
                "ChanceToDropExplosiveComponents",
                "ReducedDamageFromExplosions",
                "GainManaFromExplosionDamage",
                "MeleeDamageOnExplosionKill",
                "IncreasedBurningDamage",
                "ChanceToSpawnNapalmOnExplosion",
            },
        };

        /// <summary>All 96 of them, for a plain contains check.</summary>
        public static readonly string[] All = new string[]
        {
            "MiningDamagePercentage",
            "ChanceForExtraOre",
            "GainDurabilityOnOreWall",
            "MiningDamageAsMeleeDamage",
            "MiningSpeedIncrease",
            "ExplosivesImprovement",
            "MovementIncreaseAfterMining",
            "ChanceForRandomLootFromWall",
            "DrainLessHunger",
            "DodgeIncreaseWhileStandingStill",
            "MovementIncreaseAfterConsistentRunning",
            "MovementIncreaseAfterDodge",
            "SnareTimeReduction",
            "StackingDamageWhileRunning",
            "NearbyEnemiesSlowedDown",
            "ArmorIncreaseFromMovementSpeed",
            "MeleeAttackSpeed",
            "StackingMeleeDamage",
            "IncreaseAttackSpeedOnNextThreeAttacksChance",
            "KnockbackChance",
            "MeleeDamageIncreaseOnHittingSameTarget",
            "ChanceOnMeleeHitToIncreaseRangeDamage",
            "ChanceToRestoreHealthOnMeleeHit",
            "DamageIncreaseAgainstBosses",
            "MaxHealthIncreaseFromAllSkillPoints",
            "DamageIncreaseAtMaxHealth",
            "DamageIncreaseAtLowerHealth",
            "HealthRegenIncreaseBelowHalfHealth",
            "HealingEffectivenessIncrease",
            "HealingPotionAddHealOverTime",
            "ReducedDamageTakenFromBosses",
            "ChanceToNotDieOnLethalHit",
            "ExtraItemChanceAtBaseBuilding",
            "ToolDurabilityLastsLonger",
            "EquipmentDurabilityLastsLonger",
            "ExtraItemChanceAtAlchemyTable",
            "ExtraItemChanceAtRailsWiresAndBelts",
            "CostReductionAtAnvil",
            "PolishedItemChanceAtJewelryTable",
            "IncreasedArmorAtLowHealth",
            "RangeAttackSpeed",
            "StackingRangeDamage",
            "GuaranteedRangeCritsChance",
            "ChanceToApplySlimeOnRangeHit",
            "ChanceOnRangeHitToIncreaseMeleeDamage",
            "ChanceToStunOnRangeHit",
            "StandingStillIncreaseRangeDamage",
            "CriticalDamageMultiplier",
            "SeedFromPlantChanceIncrease",
            "GainMoreFood",
            "GainHungerFromHarvesting",
            "CriticalHitMultiplier",
            "ThornsDamageIncrease",
            "ChanceToApplyPoison",
            "ChanceToGainHigherRarityPlants",
            "DamageIncreaseAgainstPoisoned",
            "FishBitesFaster",
            "FishChanceIncrease",
            "FishStartsCloserToBeReeledIn",
            "DodgeChanceIncrease",
            "ReducedImpactOfSlipperyMovement",
            "ChanceToPreserveBait",
            "FishingAsRangeDamage",
            "IncreaseDamageWhenEatingFish",
            "GainMoreFoodFromCookedFood",
            "WellFedBuffsThresholdReduction",
            "StrongerWellFedBuffs",
            "IncreaseAttackSpeedFromCookedFood",
            "FoodBuffsLastLonger",
            "IncreaseDamageForAllNearbyAllies",
            "ChanceToGainHigherRarityCookedFood",
            "NearbyAlliesGainHealingFromCookedFood",
            "CritChance",
            "ManaOnCrit",
            "MagicBarrierDelayReduction",
            "MagicBarrierAsDamage",
            "BonusDamageOnCrit",
            "MagicBarrierAsManaRegeneration",
            "RemainingManaAsDamage",
            "ManaRegenDelayReduction",
            "MinionAttackSpeed",
            "MinionCritChance",
            "PlayerAttackSpeedPerMinion",
            "MagicDamagaAsMinionDamage",
            "MinionCountAsMagicBarrier",
            "MinionCountAsMagicDamage",
            "MinionLifespan",
            "MinionDespawnHealsPlayer",
            "IncreasedExplosiveRadius",
            "ChanceToNotConsumeExplosives",
            "ChanceToDropExplosiveComponents",
            "ReducedDamageFromExplosions",
            "GainManaFromExplosionDamage",
            "MeleeDamageOnExplosionKill",
            "IncreasedBurningDamage",
            "ChanceToSpawnNapalmOnExplosion",
        };

        private static readonly HashSet<string> Known = new HashSet<string>(All);
    }
}
