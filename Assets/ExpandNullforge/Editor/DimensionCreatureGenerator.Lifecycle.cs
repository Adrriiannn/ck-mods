using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What a creature is, how it hatches, how it dies and what it leaves.
    /// </summary>
    internal static partial class DimensionCreatureGenerator
    {
        private static void ApplyIdentity(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            if (request.IsEnemy)
            {
                EnemyAuthoring enemy = EnsureComponent<EnemyAuthoring>(root);
                enemy.dontBlockPlayerMovement = request.DontBlockPlayerMovement;
            }
            else
            {
                RemoveComponentIfPresent<EnemyAuthoring>(root);
            }

            if (request.IsBoss)
            {
                BossAuthoring boss = EnsureComponent<BossAuthoring>(root);
                DimensionBossChestTemplate chest =
                    request.BossChest ?? new DimensionBossChestTemplate();

                ObjectID chestObject = ResolveObject(chest.ChestObjectId);

                // One of the mod's own chests has no number yet; the bootstrap registers the row
                // and the link hydration writes BossCD.chestToSpawn at load. The AMOUNT has to be
                // the authored one even so — a chest whose amount stayed at zero would be written
                // in and still never appear.
                bool chestIsOneOfOurs =
                    chestObject == ObjectID.None && IsDeferred(chest.ChestObjectId);
                if (chestObject == ObjectID.None && !chestIsOneOfOurs && chest.LeavesAChest)
                {
                    report.Warnings.Add(
                        "'" + request.DisplayName + "' leaves '" + chest.ChestObjectId +
                        "' behind, which is neither one of this mod's objects nor one the game " +
                        "has, so it leaves no chest.");
                }

                boss.chestToSpawn = new ObjectData
                {
                    objectID = chestObject,
                    variation = chest.ChestVariation,
                    amount = chestObject == ObjectID.None && !chestIsOneOfOurs
                        ? 0
                        : chest.ChestAmount
                };
                boss.chestSpawnOffset = new Unity.Mathematics.float3(
                    chest.ChestOffset.x,
                    chest.ChestOffset.y,
                    chest.ChestOffset.z);
                boss.isMainStoryBoss = chest.IsAMainStoryBoss;

                boss.spawnOptionalChest = chest.LeavesASecondChest;
                if (chest.LeavesASecondChest)
                {
                    ObjectID secondChest = ResolveObject(chest.SecondChestObjectId);
                    if (secondChest == ObjectID.None &&
                        !IsDeferred(chest.SecondChestObjectId))
                    {
                        boss.spawnOptionalChest = false;
                        report.Warnings.Add(
                            "'" + request.DisplayName + "' leaves a second chest '" +
                            chest.SecondChestObjectId + "', which is neither one of this mod's " +
                            "objects nor one the game has, so it leaves only the first.");
                    }
                    else
                    {
                        boss.optionalChestVersion = new ObjectData
                        {
                            objectID = secondChest,
                            variation = chest.SecondChestVariation,
                            amount = 1
                        };
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<BossAuthoring>(root);
            }
        }

        /// <summary>
        /// Makes the creature an egg, exactly the way the Larva Hive's cocoons are: one
        /// component whose spawn target hatches out when a player nears.
        /// </summary>
        /// <remarks>
        /// The target bakes as an id when the game knows the name (a vanilla creature), and as
        /// a NAME beside it otherwise — the mod's own creatures have no ids at generation
        /// time, and the hydration system writes the real id on the first server ticks, long
        /// before anything can stand within five tiles of the egg.
        /// </remarks>
        private static void ApplyHatching(
            GameObject root,
            Request request,
            DimensionNamingContext naming,
            DimensionCreatureGenerationReport report)
        {
            DimensionHatchingTemplate hatching = request.Hatching;
            if (hatching == null || !hatching.Hatches)
            {
                RemoveComponentIfPresent<HatchWhenPlayerNearbyStateAuthoring>(root);
                RemoveComponentIfPresent<ExpandNullforge.Creatures.DimensionHatchTargetAuthoring>(root);
                return;
            }

            HatchWhenPlayerNearbyStateAuthoring hatch =
                EnsureComponent<HatchWhenPlayerNearbyStateAuthoring>(root);
            hatch.timeToHatch = hatching.SecondsToHatch;
            hatch.minSpawnAmount = hatching.HowMany.x;
            hatch.maxSpawnAmount = hatching.HowMany.y;

            ObjectID target = ResolveObject(hatching.WhatHatchesOut);
            hatch.objectToSpawn = target;
            if (target == ObjectID.None && IsDeferred(hatching.WhatHatchesOut))
            {
                // One of the mod's own; the name rides along and hydration fills the id.
                EnsureComponent<ExpandNullforge.Creatures.DimensionHatchTargetAuthoring>(root)
                    .spawnObjectName = naming.QualifyGenerated(hatching.WhatHatchesOut);
            }
            else
            {
                RemoveComponentIfPresent<ExpandNullforge.Creatures.DimensionHatchTargetAuthoring>(root);

                // A name that is neither the game's nor one of ours used to be carried anyway, so
                // the egg shipped with a qualified nonsense name and said nothing until the game
                // gave up on it hours later.
                if (target == ObjectID.None && !string.IsNullOrEmpty(hatching.WhatHatchesOut))
                {
                    report.Warnings.Add(
                        "'" + request.DisplayName + "' hatches into '" + hatching.WhatHatchesOut +
                        "', which is neither one of this mod's creatures nor one the game has, so " +
                        "nothing comes out of it.");
                }
            }
        }

        /// <summary>
        /// Everything that makes a boss FEEL like one of the game's own: fight music on the
        /// entity, a view with a floating name, a map pin object, and a summoning circle.
        /// </summary>
        /// <remarks>
        /// Runs inside GenerateOne's try, before the boss prefab saves, because the view prefab
        /// reference must land in <c>graphicalPrefab</c> on the root that is about to be saved.
        /// The companion prefabs pause the asset batch for their own saves — inside a batch,
        /// SaveAsPrefabAsset returns null, and here the returned reference is the point.
        /// </remarks>
        private static void ApplyBossExtras(
            GameObject root,
            Request request,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionCreatureGenerationReport report)
        {
            DimensionObjectSpine.ApplyMusicArea(
                root,
                request.FightMusic,
                message => report.Warnings.Add("'" + request.DisplayName + "' " + message));

            DimensionBossMapPinTemplate pin = request.MapPin ?? new DimensionBossMapPinTemplate();
            if (pin.PinGoesWhenItDies)
            {
                // Not left to the SimpleTraits toggle: a pin that survives its boss is the
                // silent failure, so bossness itself carries the death link.
                EnsureComponent<DisableMapMarkerOnDeathAuthoring>(root);
            }

            if (pin.ShowsOnTheMap)
            {
                BuildBossMapMarkerPrefab(request, pin, outputFolder, naming, report);
            }

            if (!string.IsNullOrEmpty(request.SummoningItemId))
            {
                BuildBossSummonCirclePrefab(request, outputFolder, naming, report);
            }
        }

        private static void ApplyDeath(GameObject root, Request request)
        {
            DimensionCreatureStatsTemplate stats = request.Stats;
            if (!stats.OverrideDeathTiming && !stats.SkipDeathAnimation)
            {
                return;
            }

            DeathStateAuthoring death = EnsureComponent<DeathStateAuthoring>(root);
            death.overrideTimeBeforeDestroy = stats.OverrideDeathTiming;
            if (stats.OverrideDeathTiming)
            {
                death.timeBeforeDestroy = stats.TimeBeforeDestroy;
                death.timeBeforeLootDrop = stats.TimeBeforeLootDrop;
            }

            death.skipDeathAnimation = stats.SkipDeathAnimation;
        }

        /// <summary>
        /// Points the creature at the Core Keeper behaviour it borrows.
        /// </summary>
        /// <remarks>
        /// An unrecognised name is reported rather than guessed at. Falling back to some default AI
        /// would produce a creature that spawns, moves and fights — just not remotely as intended,
        /// which is far harder to notice than one that never spawns.
        /// </remarks>
        private static void ApplyBehaviour(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            if (string.IsNullOrEmpty(request.BehaviourName))
            {
                RemoveComponentIfPresent<BehaviourAuthoring>(root);
                return;
            }

            BehaviourObjectID behaviourId;
            if (!Enum.TryParse(request.BehaviourName, false, out behaviourId))
            {
                report.Warnings.Add(
                    request.CreatureId + " asks for behaviour '" + request.BehaviourName +
                    "', which this version of the game does not have. It will spawn with no behaviour " +
                    "and stand still.");
                RemoveComponentIfPresent<BehaviourAuthoring>(root);
                return;
            }

            BehaviourAuthoring behaviour = EnsureComponent<BehaviourAuthoring>(root);
            behaviour.objectID = behaviourId;

            // Only the robot patroller behaviour reads these, and it reads them off the component
            // rather than off its own mortar states — so a creature borrowing that behaviour had no
            // way to change what its mortars did.
            DimensionCreatureCombatTemplate combat = request.Combat;
            if (combat != null)
            {
                behaviour.robotPatrollerBehaviourSettings =
                    new BehaviourAuthoring.RobotPatrollerBehaviourSettings
                    {
                        oilMortarDamage = combat.OilMortarFlatDamage,
                        oilMortarDamageMultiplier = combat.OilMortarMultiplier,
                        oilMortarTileDamage = combat.OilMortarFlatTerrainDamage,
                        oilMortarTileDamageMultiplier = combat.OilMortarTerrainMultiplier,
                        fireMortarDamage = combat.FireMortarFlatDamage,
                        fireMortarDamageMultiplier = combat.FireMortarMultiplier,
                        fireMortarTileDamage = combat.FireMortarFlatTerrainDamage,
                        fireMortarTileDamageMultiplier = combat.FireMortarTerrainMultiplier
                    };
            }
        }

        /// <summary>
        /// What this creature drops: its own loot table, plus anything that named it as a source.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two independent things, and a creature may have either or both. The loot table is the
        /// creature's own; the custom loot is the other half of the drop-location inversion — every
        /// item that said "I drop off this" collected into one place by
        /// <c>DimensionDropCollector</c>.
        /// </para>
        /// <para>
        /// Drops the emitter cannot carry — an amount range or a biome restriction, neither of which
        /// per-object custom loot supports — are handed back and put on the report, so the bootstrap
        /// step can register them against the creature's loot table at load instead. They must not be
        /// dropped here: silently losing a range is exactly the failure the routing exists to prevent.
        /// </para>
        /// </remarks>
        private static void ApplyLoot(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            bool hasTable = request.LootTable != null;
            bool hasCollectedDrops = request.DropsFromItems != null &&
                request.DropsFromItems.Drops.Count > 0;

            // TAKEN BACK ABOVE THE EARLY RETURN. It used to sit below it, so a creature that had
            // its loot table and its drops cleared but still shed ore kept last generate's table
            // stamped on it — and the "you are using the game's own table" warning then named a
            // table the author had already deleted.
            DimensionDropEmitter.ClearAnyLootTheLastGenerateWrote(root);

            // Pet and extra loot run BEFORE the no-loot early return. A pet mob without a
            // death loot table is the normal shape of a pet mob, and both used to sit below
            // the return — so a creature with no loot silently never became a pet and never
            // carried its extra loot, with no warning anywhere.
            DimensionObjectSpine.ApplyPet(
                root,
                request.Pet,
                delegate(string message)
                {
                    report.Warnings.Add("'" + request.DisplayName + "' " + message);
                });

            if (!hasTable && !hasCollectedDrops)
            {
                DimensionObjectSpine.ApplyExtraLoot(
                    root,
                    request.ExtraLoot,
                    delegate(string objectId) { return ResolveObject(objectId); },
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    },
                    IsDeferred);

                // The removal is CONDITIONAL now. Extra loot may have just added the component —
                // shedding ore as you mine it is the normal shape of a creature with no death loot
                // table — and taking it straight back off threw that away a line after it was
                // written, with nothing said.
                if (request.ExtraLoot == null || !request.ExtraLoot.WantsAnExtraChannel)
                {
                    RemoveComponentIfPresent<DropLootAuthoring>(root);
                }
                return;
            }

            DropLootAuthoring loot = EnsureComponent<DropLootAuthoring>(root);

            DimensionObjectSpine.ApplyExtraLoot(
                root,
                request.ExtraLoot,
                delegate(string objectId) { return ResolveObject(objectId); },
                delegate(string message)
                {
                    report.Warnings.Add("'" + request.DisplayName + "' " + message);
                },
                IsDeferred);

            if (hasTable)
            {
                // The mod's own table's id is minted from the name by pure arithmetic, so the
                // prefab can carry it now and the runtime registry builds the table under the same
                // id at load — the two never need to meet. LootTableIdFor is the shared answer the
                // bootstrap emitter also ships, so a drop row and this prefab cannot disagree.
                string tableName = DimensionDropEmitter.AuthoredLootTableNameOf(request.LootTable);
                if (!string.IsNullOrEmpty(tableName))
                {
                    loot.hasLootTable = true;
                    loot.lootTableID = DimensionDropEmitter.LootTableIdFor(tableName);
                }
                else if (!string.IsNullOrEmpty(request.LootTable.LootTableId))
                {
                    report.Warnings.Add(
                        "'" + request.DisplayName + "' points at loot table '" +
                        request.LootTable.LootTableId + "', which is neither one of the game's nor " +
                        "one of yours with anything in it, so it drops nothing from a table. Put " +
                        "rows in that table, or point at one of the game's.");
                }
            }

            if (!hasCollectedDrops)
            {
                return;
            }

            // A creature that points at a loot table shares it: the drops go into a table this mod
            // does not get to reshape, so a chance under 1 cannot be made true there and the report
            // has to say so rather than promise the number works.
            List<DimensionResolvedDrop> needsATable = DimensionDropEmitter.ApplyCustomLoot(
                root,
                request.DropsFromItems,
                delegate(string itemId) { return ResolveObject(itemId); },
                delegate(string message) { report.Warnings.Add(message); },
                IsDeferred,
                !loot.hasLootTable);

            for (int i = 0; i < needsATable.Count; i++)
            {
                report.DropsNeedingALootTable.Add(needsATable[i]);
            }

            // A creature with drops that only a loot table can carry NEEDS a loot table, and most
            // creatures are authored without one. Given none it used to carry no
            // DropsLootFromLootTableCD at all, so the drop had nowhere to be registered at load and
            // simply never happened.
            if (needsATable.Count > 0)
            {
                DimensionDropEmitter.EnsureALootTableToHangDropsOn(
                    root,
                    binder.Naming.QualifyGenerated(request.CreatureId),
                    delegate(string message) { report.Warnings.Add(message); });
            }
        }
    }
}
