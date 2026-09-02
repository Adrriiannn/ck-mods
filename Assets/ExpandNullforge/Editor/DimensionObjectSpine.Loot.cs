using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The extra loot an object drops beyond the one table it names.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        public static void ApplyExtraLoot(
            GameObject root,
            DimensionExtraLootTemplate extra,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (root == null || extra == null)
            {
                return;
            }

            // Unlike the other shape templates this one MAY add the component. An object can drop
            // nothing on death and still shed ore as you mine it, and refusing to add DropLoot here
            // would make that object unauthorable.
            bool wantsAnyChannel = extra.DropsLootAsItIsHit
                || extra.DropsLootWhenUsed
                || extra.DropsDifferentLootInSeason;
            DropLootAuthoring loot = root.GetComponent<DropLootAuthoring>();
            if (loot == null)
            {
                if (!wantsAnyChannel)
                {
                    return;
                }

                loot = EnsureComponent<DropLootAuthoring>(root);
            }

            // ---- as it is hit ----
            loot.hasLootDropsOnTakingDamage = extra.DropsLootAsItIsHit && !extra.ShedsNothing;
            if (loot.hasLootDropsOnTakingDamage)
            {
                // LootDropsWhenDamaged is a struct, so the field is always there to write into.
                loot.lootDropsWhenDamaged.dropsLoot = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(extra.ShedsObjectId);
                loot.lootDropsWhenDamaged.damageToDealToDropLoot = extra.DamageNeededToShed;
                loot.lootDropsWhenDamaged.healthPercentageDamageToDeal =
                    extra.HealthShareNeededToShed;
                loot.lootDropsWhenDamaged.minHealthToDropLoot = extra.StopsSheddingBelowHealth;
                loot.lootDropsWhenDamaged.minHealthPercentageToDropLoot =
                    extra.StopsSheddingBelowHealthShare;
                loot.lootDropsWhenDamaged.instantiateEntity = extra.ShedsALiveObject;
                loot.lootDropsWhenDamaged.minSpawnOffset = new Unity.Mathematics.float2(
                    extra.ShedLandsFrom.x,
                    extra.ShedLandsFrom.y);
                loot.lootDropsWhenDamaged.maxSpawnOffset = new Unity.Mathematics.float2(
                    extra.ShedLandsTo.x,
                    extra.ShedLandsTo.y);
                loot.lootDropsWhenDamaged.maxLimitToDropInNearbyArea =
                    extra.StopsAfterThisManyNearby;

                bool shedIsOneOfOurs =
                    loot.lootDropsWhenDamaged.dropsLoot == ObjectID.None &&
                    isDeferred != null &&
                    isDeferred(extra.ShedsObjectId);

                if (loot.lootDropsWhenDamaged.dropsLoot == ObjectID.None && !shedIsOneOfOurs)
                {
                    loot.hasLootDropsOnTakingDamage = false;
                    if (report != null)
                    {
                        report(
                            "sheds '" + extra.ShedsObjectId + "' as it is hit, which is neither " +
                            "one of this mod's objects nor one the game has, so it sheds nothing.");
                    }
                }
                else if (root.GetComponent<HealthAuthoring>() == null)
                {
                    // NO HEALTH, NO SHED — for a vanilla target as much as for one of ours.
                    // DropLootConverter works out how much damage a shed costs from the health
                    // pool, and with none it logs a red error and returns before writing the shed
                    // (ck-db\Pug.ECS.Conversion\DropLootConverter.cs:98-104). Only the shed itself
                    // is lost — death loot, the loot table, seasonal loot and on-use loot are all
                    // written before that return — but the error names the creator's prefab and
                    // the shed never happens either way, so it is switched off here with a sentence
                    // that says what to do. The check used to fire only for one of our own targets,
                    // which left the common case hitting the game's error with nothing said.
                    loot.hasLootDropsOnTakingDamage = false;
                    if (report != null)
                    {
                        report(
                            "sheds '" + extra.ShedsObjectId + "' as it is hit but has no health " +
                            "pool, and the game works out how much damage a shed costs from that. " +
                            "Give it health, or make it breakable, and generate again.");
                    }
                }

                // The flag STAYS TRUE for one of the mod's own objects. DropLootConverter writes
                // DropsLootWhenDamagedCD only while hasLootDropsOnTakingDamage is set
                // (ck-db\Pug.ECS.Conversion\DropLootConverter.cs:98), so turning it off here leaves
                // no component for the link hydration to fill in and the shed is lost for good.
            }

            // ---- when it is used ----
            loot.hasLootDropsOnUse = extra.DropsLootWhenUsed && !extra.GivesNothingOnUse;
            if (loot.hasLootDropsOnUse)
            {
                // OnUseLootDrops is a struct too.
                // THE TABLE IS WRITTEN EVERY TIME, THE CLEARED ONE INCLUDED. It used to be written
                // only inside the guard, with no else, so an author who set a table, generated,
                // then cleared the table while keeping items in "gives on use" went on handing out
                // the old table forever. A misspelling took the same road and said nothing.
                LootTableID useTable;
                if (string.IsNullOrEmpty(extra.UseLootTableId))
                {
                    loot.onUseLootDrops.lootTableID = default(LootTableID);
                }
                else if (DimensionEditorLootTables.TryResolve(extra.UseLootTableId, out useTable))
                {
                    loot.onUseLootDrops.lootTableID = useTable;
                }
                else
                {
                    loot.onUseLootDrops.lootTableID = default(LootTableID);
                    if (report != null)
                    {
                        report(
                            "gives loot table '" + extra.UseLootTableId + "' when used, which is " +
                            "neither one of this mod's tables nor one the game has, so using it " +
                            "hands out nothing from a table.");
                    }
                }

                EffectID useEffect;
                if (string.IsNullOrEmpty(extra.UseEffectId))
                {
                    loot.onUseLootDrops.spawnEffects = default(EffectID);
                }
                else if (System.Enum.TryParse(extra.UseEffectId, false, out useEffect))
                {
                    loot.onUseLootDrops.spawnEffects = useEffect;
                }
                else
                {
                    loot.onUseLootDrops.spawnEffects = default(EffectID);
                    if (report != null)
                    {
                        report(
                            "shows '" + extra.UseEffectId + "' when used, which is not an effect " +
                            "the game has, so nothing is shown.");
                    }
                }

                loot.onUseLootDrops.lootDrops =
                    new System.Collections.Generic.List<OnUseLootDrop>();
                DimensionLootEntry[] onUse = extra.GivesOnUse;
                for (int i = 0; i < onUse.Length; i++)
                {
                    ObjectID given = resolveObject == null
                        ? ObjectID.None
                        : resolveObject(onUse[i].ObjectId);
                    if (given == ObjectID.None)
                    {
                        if (report != null)
                        {
                            report(
                                "gives '" + onUse[i].ObjectId + "' when used, which the game does " +
                                "not have, so that one is left out.");
                        }

                        continue;
                    }

                    loot.onUseLootDrops.lootDrops.Add(new OnUseLootDrop
                    {
                        lootDropID = given,
                        amount = onUse[i].Amount,
                        chance = onUse[i].Chance
                    });
                }
            }

            // ---- by season ----
            loot.hasSeasonalLoot = extra.DropsDifferentLootInSeason && !extra.SeasonHasNoDrops;
            if (loot.hasSeasonalLoot)
            {
                Season whichSeason;
                if (!System.Enum.TryParse(extra.Season, false, out whichSeason))
                {
                    loot.hasSeasonalLoot = false;
                    if (report != null)
                    {
                        report(
                            "drops seasonal loot during '" + extra.Season + "', which is not a " +
                            "season the game has, so its seasonal drops never happen.");
                    }
                }
                else
                {
                    if (loot.seasonalLootDrops == null)
                    {
                        loot.seasonalLootDrops = new SeasonAndLoots();
                    }

                    SeasonAndLoot forThisSeason = new SeasonAndLoot
                    {
                        season = whichSeason,
                        lootDrops = new System.Collections.Generic.List<SeasonalLootDrop>()
                    };

                    DimensionLootEntry[] seasonal = extra.SeasonalDrops;
                    for (int i = 0; i < seasonal.Length; i++)
                    {
                        ObjectID dropped = resolveObject == null
                            ? ObjectID.None
                            : resolveObject(seasonal[i].ObjectId);
                        if (dropped == ObjectID.None)
                        {
                            if (report != null)
                            {
                                report(
                                    "drops '" + seasonal[i].ObjectId + "' in season, which the " +
                                    "game does not have, so that one is left out.");
                            }

                            continue;
                        }

                        forThisSeason.lootDrops.Add(new SeasonalLootDrop
                        {
                            lootDropID = dropped,
                            amount = seasonal[i].Amount,
                            chance = seasonal[i].Chance,
                            multiplayerAmountAdditionScaling = seasonal[i].ExtraPerPlayer
                        });
                    }

                    loot.seasonalLootDrops.lootDrops =
                        new System.Collections.Generic.List<SeasonAndLoot> { forThisSeason };
                }
            }

            if (report == null)
            {
                return;
            }

            if (extra.ShedsWithNoThreshold)
            {
                report(
                    "sheds loot as it is hit with no damage threshold to shed at, so every point " +
                    "of damage sheds one. Give it a damage amount or a health share.");
            }

            if (extra.GivesNothingOnUse)
            {
                report(
                    "is set to give something when used with neither a loot table nor a list of " +
                    "what to give, so using it does nothing.");
            }

            if (extra.SeasonHasNoDrops)
            {
                report("names a season to drop different loot in with nothing to drop in it.");
            }
        }
    }
}
