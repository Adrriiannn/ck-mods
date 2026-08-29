using System.Text;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Emits one row per reference in the mod's content that names one of the mod's OWN objects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHY THESE NEED ANY RUNTIME CODE AT ALL, given that everything else about an item is prefab
    /// data. The fields behind them are raw <c>ObjectID</c> enums — the shot a bow fires, the thing
    /// a scanner points at, what a piece of jewellery polishes into — and a mod's object ids do not
    /// exist while its prefabs are being written. When the reference is one of the GAME's own
    /// objects the generator bakes the number and nothing is emitted here; when it is one of the
    /// mod's own, this row is what turns the two names into the two numbers at load.
    /// </para>
    /// <para>
    /// IT WALKS THE TEMPLATE, NOT WHAT THE GENERATORS DID. <c>EnsureGeneratedRuntime</c> is
    /// reachable without any object generator having run — the Portal Studio's "apply visual
    /// changes" calls it — so a static list filled by the generators would silently emit nothing on
    /// that path and every deferred reference in the mod would go quiet. The generator and this walk
    /// share one ownership predicate (<c>DimensionObjectBinder.IsDeferred</c> over the ids from
    /// <c>DimensionGeneratedObjectIds.Collect</c>) so the two cannot disagree about which references
    /// were deferred; a test asserts it.
    /// </para>
    /// <para>
    /// EVERY MEMBER OF <see cref="DimensionObjectLink"/> IS PRODUCED HERE. That is the whole point
    /// of the enum being a census: an arm nothing emits is unreachable code wearing a doc comment.
    /// The five walks below — items, projectiles, creatures, containers, world objects — cover the
    /// five kinds of asset that carry one of these fields, and a test drives the emitter over a
    /// template exercising all of them and fails if any member produces no row.
    /// </para>
    /// <para>
    /// The registry name is written out in full rather than relying on a <c>using</c> in the
    /// generated file's header, matching the explosive rows next to it.
    /// </para>
    /// </remarks>
    internal static partial class DimensionRuntimeConsumerBootstrapUtility
    {
        internal static void AppendObjectLinkRegistrations(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName)
        {
            if (template == null)
            {
                return;
            }

            // Built from the mod name it was handed rather than resolved from a folder: this runs
            // from the manifest export, which has no output folder, and an AssetDatabase lookup here
            // would be a second answer to a question the caller has already answered.
            DimensionObjectBinder binder = new DimensionObjectBinder(
                new DimensionNamingContext(
                    modName, DimensionGeneratedObjectIds.Collect(template)),
                DimensionGeneratedObjectIds.SwitchedOff(template));

            AppendItemLinks(builder, template, modName, binder);
            AppendProjectileLinks(builder, template, modName, binder);
            AppendCreatureLinks(builder, template, modName, binder);
            AppendContainerLinks(builder, template, modName, binder);
            AppendWorldObjectLinks(builder, template, modName, binder);
        }

        /// <summary>Everything an item points at that the item generator left as a name.</summary>
        private static void AppendItemLinks(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName,
            DimensionObjectBinder binder)
        {
            DimensionItemAsset[] items = template.GlobalItems;
            for (int i = 0; items != null && i < items.Length; i++)
            {
                DimensionItemAsset item = items[i];
                if (item == null || !item.Enabled || string.IsNullOrEmpty(item.ItemId))
                {
                    continue;
                }

                string owner = DimensionObjectNamespace.Qualify(modName, item.ItemId);

                DimensionWeaponTemplate weapon = item.Weapon;
                if (weapon != null && weapon.IsAWeapon && weapon.IsRanged)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.FiresProjectile, weapon.FiresProjectileId, binder);

                    // The explosion size travels with the wound-up shot. The generator has to bake
                    // it as zero while the shot is deferred, or vanilla's RangeWeaponConverter logs
                    // a red error naming the creator's prefab on a setup that is entirely correct.
                    Row(builder, modName, owner,
                        DimensionObjectLink.SecondProjectile, weapon.SecondProjectileId, binder,
                        -1, 0, weapon.ExplosionSize);

                    // The index has to be counted the way the generator counts it: an entry that
                    // names nothing at all is dropped from the authoring list, so the position in
                    // the built list is NOT the position in the authored array.
                    string[] randomIds = weapon.RandomProjectileIds;
                    int position = 0;
                    for (int r = 0; r < randomIds.Length; r++)
                    {
                        bool baked = DimensionObjectBinder.Vanilla(randomIds[r]) != ObjectID.None;
                        bool deferred = binder.IsDeferred(randomIds[r]);
                        if (!baked && !deferred)
                        {
                            continue;
                        }

                        if (deferred)
                        {
                            Row(builder, modName, owner,
                                DimensionObjectLink.RandomProjectile, randomIds[r], binder, position);
                        }

                        position++;
                    }
                }

                if (weapon != null && weapon.IsAWeapon && weapon.Beam != null &&
                    weapon.Beam.FiresABeam)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.BeamProjectile, weapon.Beam.SecondProjectileId, binder);
                }

                DimensionSecondaryUseTemplate secondary = item.SecondaryUse;
                if (secondary != null && secondary.HasSecondaryUse && secondary.SummonsMinion)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.SecondaryUseMinion, secondary.MinionObjectId, binder);
                }

                Row(builder, modName, owner,
                    DimensionObjectLink.PolishesInto, item.PolishesInto, binder);
                Row(builder, modName, owner,
                    DimensionObjectLink.ScansFor, item.ScansForObjectId, binder);

                // The health check is the generator's, asked through the one property both sides
                // read. Without a health pool the object cannot be hit at all, so the generator
                // switches shedding off — and a row for a field that will not exist would land on
                // "impossible" at load and tell the creator to tick something already ticked.
                DimensionExtraLootTemplate extra = item.ExtraLoot;
                if (extra != null && extra.DropsLootAsItIsHit && !extra.ShedsNothing &&
                    item.GetsAHealthPool)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.ShedsWhenDamaged, extra.ShedsObjectId, binder);
                }

                DimensionWorldRolesTemplate roles = item.WorldRoles;
                if (roles != null && roles.IsATitanShrine && !roles.ShrineHasNoTitan)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.TitanShrine, roles.BoundTitanId, binder);
                }
            }
        }

        /// <summary>What a shot breaks into, and what it leaves where it lands.</summary>
        private static void AppendProjectileLinks(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName,
            DimensionObjectBinder binder)
        {
            DimensionProjectileAsset[] projectiles = template.GlobalProjectiles;
            for (int i = 0; projectiles != null && i < projectiles.Length; i++)
            {
                DimensionProjectileAsset projectile = projectiles[i];
                if (projectile == null || !projectile.Enabled ||
                    string.IsNullOrEmpty(projectile.ProjectileId))
                {
                    continue;
                }

                string owner = DimensionObjectNamespace.Qualify(modName, projectile.ProjectileId);

                // Only the shot that actually shatters carries a shard component to write into;
                // ProjectileConverter adds ShatterOnCollisionProjectileCD behind that flag.
                if (projectile.ShattersOnCollision)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.ProjectileShard, projectile.ShardObjectId, binder);
                }

                // Only an arcing shell carries MortarProjectileDamageEffectCD; a straight shot is
                // given MortarProjectileAuthoring removed rather than added.
                if (projectile.ArcsLikeArtillery)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.ProjectileNapalm, projectile.LeavesBehindObjectId,
                        binder, -1, projectile.LeavesBehindVariation);
                }
            }
        }

        /// <summary>
        /// What a creature shoots, what a boss leaves behind, and how a long body ends.
        /// </summary>
        /// <remarks>
        /// A creature's shot is <c>RangeAttackStateAuthoring.projectileID</c>, which is a different
        /// component from a bow's — hence <see cref="DimensionObjectLink.CreatureShot"/> rather than
        /// reusing the weapon member. "My creature shoots my projectile" is the most obvious thing
        /// anybody would try, and it was the one the framework could not do.
        /// </remarks>
        private static void AppendCreatureLinks(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName,
            DimensionObjectBinder binder)
        {
            DimensionMobAsset[] mobs = template.GlobalMobs;
            for (int i = 0; mobs != null && i < mobs.Length; i++)
            {
                DimensionMobAsset mob = mobs[i];
                if (mob == null || !mob.Enabled || string.IsNullOrEmpty(mob.MobId))
                {
                    continue;
                }

                string owner = DimensionObjectNamespace.Qualify(modName, mob.MobId);
                CreatureShotRow(builder, modName, owner, mob.Combat, binder);
                SegmentRows(builder, modName, owner, mob.Combat, binder);
                ShedRow(builder, modName, owner, mob.ExtraLoot, binder);

                // THE ELITE IS A SECOND CREATURE and needs its own rows. It is generated under
                // "<mobId>.elite" from the same combat and extra-loot blocks, so the base mob fired
                // the mod's projectile and its elite fired nothing — forever, with no warning at
                // generate and no row to reach the settling report either.
                DimensionEliteVariantTemplate elite = mob.EliteVariant;
                if (elite != null && elite.Enabled)
                {
                    string eliteOwner = DimensionObjectNamespace.Qualify(
                        modName, DimensionEliteVariantTemplate.IdFor(mob.MobId));
                    CreatureShotRow(builder, modName, eliteOwner, mob.Combat, binder);
                    SegmentRows(builder, modName, eliteOwner, mob.Combat, binder);
                    ShedRow(builder, modName, eliteOwner, mob.ExtraLoot, binder);
                }
            }

            DimensionBossAsset[] bosses = template.GlobalBosses;
            for (int i = 0; bosses != null && i < bosses.Length; i++)
            {
                DimensionBossAsset boss = bosses[i];
                if (boss == null || !boss.Enabled || string.IsNullOrEmpty(boss.BossId))
                {
                    continue;
                }

                string owner = DimensionObjectNamespace.Qualify(modName, boss.BossId);
                CreatureShotRow(builder, modName, owner, boss.Combat, binder);
                SegmentRows(builder, modName, owner, boss.Combat, binder);

                DimensionBossChestTemplate chest = boss.BossChest;
                if (chest == null)
                {
                    continue;
                }

                if (chest.LeavesAChest)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.BossChest, chest.ChestObjectId, binder,
                        -1, chest.ChestVariation);
                }

                if (chest.LeavesASecondChest)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.BossSecondChest, chest.SecondChestObjectId, binder,
                        -1, chest.SecondChestVariation);
                }
            }

            DimensionAnimalAsset[] animals = template.GlobalAnimals;
            for (int i = 0; animals != null && i < animals.Length; i++)
            {
                DimensionAnimalAsset animal = animals[i];
                if (animal == null || !animal.Enabled || string.IsNullOrEmpty(animal.AnimalId))
                {
                    continue;
                }

                CreatureShotRow(
                    builder, modName,
                    DimensionObjectNamespace.Qualify(modName, animal.AnimalId),
                    animal.Combat, binder);
            }
        }

        private static void CreatureShotRow(
            StringBuilder builder,
            string modName,
            string owner,
            DimensionCreatureCombatTemplate combat,
            DimensionObjectBinder binder)
        {
            if (combat == null || !combat.HasRanged)
            {
                return;
            }

            Row(builder, modName, owner,
                DimensionObjectLink.CreatureShot, combat.ProjectileItemId, binder);
        }

        /// <summary>
        /// What a creature sheds as it is hit, when that is one of the mod's own objects.
        /// </summary>
        /// <remarks>
        /// No health check here, unlike the item walk: every generated creature is given a health
        /// pool, so the component the shed needs always exists.
        /// </remarks>
        private static void ShedRow(
            StringBuilder builder,
            string modName,
            string owner,
            DimensionExtraLootTemplate extra,
            DimensionObjectBinder binder)
        {
            if (extra == null || !extra.DropsLootAsItIsHit || extra.ShedsNothing)
            {
                return;
            }

            Row(builder, modName, owner,
                DimensionObjectLink.ShedsWhenDamaged, extra.ShedsObjectId, binder);
        }

        private static void SegmentRows(
            StringBuilder builder,
            string modName,
            string owner,
            DimensionCreatureCombatTemplate combat,
            DimensionObjectBinder binder)
        {
            DimensionSegmentedCreatureTemplate segmented =
                combat == null ? null : combat.Segmented;
            if (segmented == null || !segmented.IsSegmented || segmented.HasNoBody)
            {
                return;
            }

            Row(builder, modName, owner,
                DimensionObjectLink.SegmentTail, segmented.TailObjectId, binder);
            Row(builder, modName, owner,
                DimensionObjectLink.NeverHits, segmented.NeverHits, binder);
        }

        /// <summary>What a chest reacts to, what it turns into, and what its slots accept.</summary>
        /// <remarks>
        /// The slot rule is the one link that is a list inside a list: <c>slotIndex</c> is the
        /// rule's position, which <c>InventoryConverter</c> writes as the position in
        /// <c>slotRequirements</c>, and the entry index is the position inside that rule's accepted
        /// ids. Both sides count deferred entries as present, which is why the generator writes a
        /// <c>None</c> placeholder rather than dropping them.
        /// </remarks>
        private static void AppendContainerLinks(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName,
            DimensionObjectBinder binder)
        {
            DimensionContainerAsset[] containers = template.GlobalContainers;
            for (int i = 0; containers != null && i < containers.Length; i++)
            {
                DimensionContainerAsset container = containers[i];
                if (container == null || !container.Enabled ||
                    string.IsNullOrEmpty(container.ContainerId))
                {
                    continue;
                }

                string owner = DimensionObjectNamespace.Qualify(modName, container.ContainerId);

                if (container.ReactsToContents)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.ContainerReactsTo, container.ReactsToItemId, binder);

                    if (container.BecomesSomethingElse)
                    {
                        Row(builder, modName, owner,
                            DimensionObjectLink.ContainerBecomes, container.BecomesContainerId,
                            binder);
                    }
                }

                DimensionMelodyResponseTemplate chestMelody = container.MelodyResponse;
                if (chestMelody != null && chestMelody.HasAnySetting &&
                    chestMelody.BecomesADifferentObject && !chestMelody.BecomesNothing)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.MelodyAffects, chestMelody.BecomesObjectId, binder);
                }

                DimensionContainerSlotRule[] rules = container.EffectiveSlotRules;
                for (int r = 0; rules != null && r < rules.Length; r++)
                {
                    string[] accepted = rules[r].AcceptsItemIds;
                    int entry = 0;
                    for (int a = 0; accepted != null && a < accepted.Length; a++)
                    {
                        // The game's own cap on a slot rule, obeyed here exactly as the container
                        // generator obeys it. Counting past it wrote rows for positions the baked
                        // rule does not have, and the load-time repair for that says "generate
                        // again" — advice that cannot work, because the cap is the cause.
                        if (entry >= DimensionContainerSlotRule.MaxAcceptedItemIds)
                        {
                            break;
                        }

                        bool baked = DimensionObjectBinder.Vanilla(accepted[a]) != ObjectID.None;
                        bool deferred = binder.IsDeferred(accepted[a]);
                        if (!baked && !deferred)
                        {
                            continue;
                        }

                        if (deferred)
                        {
                            Row(builder, modName, owner,
                                DimensionObjectLink.SlotAccepts, accepted[a], binder,
                                r, 0, 0, entry);
                        }

                        entry++;
                    }
                }
            }
        }

        /// <summary>
        /// What a world object can point at: its plant, its spawner's creature, its trophy's
        /// creature, what a melody turns it into, what it leaves as a trail, its shop, the titan
        /// its shrine is bound to, what it sheds as it is hit, and what right-clicking it summons.
        /// </summary>
        /// <remarks>
        /// The last three arrived late. The generators had been deferring all three for a world
        /// object with nothing emitting a row for any of them, which is the one shape the enum
        /// census cannot see: a member with a producer somewhere else reads as covered.
        /// </remarks>
        private static void AppendWorldObjectLinks(
            StringBuilder builder,
            DimensionTemplateAsset template,
            string modName,
            DimensionObjectBinder binder)
        {
            DimensionWorldObjectAsset[] worldObjects = template.GlobalWorldObjects;
            for (int i = 0; worldObjects != null && i < worldObjects.Length; i++)
            {
                DimensionWorldObjectAsset worldObject = worldObjects[i];
                if (worldObject == null || !worldObject.Enabled ||
                    string.IsNullOrEmpty(worldObject.ObjectIdentifier))
                {
                    continue;
                }

                string owner =
                    DimensionObjectNamespace.Qualify(modName, worldObject.ObjectIdentifier);

                Row(builder, modName, owner,
                    DimensionObjectLink.FlowerOfPlant, worldObject.FlowerOfPlantId, binder,
                    -1, worldObject.FlowerVariation);
                Row(builder, modName, owner,
                    DimensionObjectLink.SpawnerEnemy, worldObject.SpawnsEnemyId, binder);

                if (worldObject.Kind == DimensionWorldObjectKind.Trophy)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.TrophyEnemy, worldObject.SummonsEnemyId, binder);
                }

                DimensionMelodyResponseTemplate melody = worldObject.MelodyResponse;
                if (melody != null && melody.HasAnySetting && melody.BecomesADifferentObject &&
                    !melody.BecomesNothing)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.MelodyAffects, melody.BecomesObjectId, binder);
                }

                DimensionChainReactionTemplate chain = worldObject.ChainReaction;
                if (chain != null && chain.LeavesATrail && !chain.TrailOfNothing)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.TrailObject, chain.TrailObjectId, binder);
                }

                // A world object can be a shrine too. The generator already defers a titan of the
                // mod's own here, silently — and with no row it stayed None forever and never even
                // reached the settling report, so the one thing that could have said so never ran.
                DimensionWorldRolesTemplate worldRoles = worldObject.WorldRoles;
                if (worldRoles != null && worldRoles.IsATitanShrine && !worldRoles.ShrineHasNoTitan)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.TitanShrine, worldRoles.BoundTitanId, binder);
                }

                // What it sheds as it is hit. Gated on being attackable at all, the same way the
                // item walk gates on a health pool: DropLootConverter refuses the shed channel
                // without HealthAuthoring, and the generator takes health off anything that cannot
                // be attacked, so a row there would land on "impossible" at load.
                if (!worldObject.CannotBeAttacked)
                {
                    ShedRow(builder, modName, owner, worldObject.ExtraLoot, binder);
                }

                DimensionSecondaryUseTemplate placedSecondary = worldObject.SecondaryUse;
                if (placedSecondary != null && placedSecondary.HasSecondaryUse &&
                    placedSecondary.SummonsMinion)
                {
                    Row(builder, modName, owner,
                        DimensionObjectLink.SecondaryUseMinion, placedSecondary.MinionObjectId,
                        binder);
                }

                DimensionTraderTemplate trader = worldObject.Trader;
                if (trader == null || !trader.IsATrader || trader.TradesWithNothingToSell)
                {
                    continue;
                }

                DimensionTradeGood[] stock = trader.Stock;
                int line = 0;
                for (int s = 0; stock != null && s < stock.Length; s++)
                {
                    bool baked = DimensionObjectBinder.Vanilla(stock[s].ObjectId) != ObjectID.None;
                    bool deferred = binder.IsDeferred(stock[s].ObjectId);
                    if (!baked && !deferred)
                    {
                        continue;
                    }

                    if (deferred)
                    {
                        Row(builder, modName, owner,
                            DimensionObjectLink.MerchantStock, stock[s].ObjectId, binder, line);
                    }

                    line++;
                }
            }
        }

        /// <summary>
        /// Writes one row, or nothing at all when the reference did not need deferring.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A reference to one of the game's own objects is already a number on the prefab. Emitting
        /// a row for it would make the runtime look up something it does not need, and would bury
        /// the case where a name really cannot be resolved under a busy log.
        /// </para>
        /// <para>
        /// A reference to one of the mod's own assets that is SWITCHED OFF is neither: no object
        /// will ever answer to it, so there is nothing to defer, and the creator is told here rather
        /// than left to notice in game that the bow fires nothing.
        /// </para>
        /// <para>
        /// The owner variation is written out as zero rather than left to the default. Every object
        /// this framework generates is registered at variation zero, and a row that quietly defaulted
        /// would be indistinguishable from one that had thought about it.
        /// </para>
        /// </remarks>
        private static void Row(
            StringBuilder builder,
            string modName,
            string ownerObjectName,
            DimensionObjectLink link,
            string targetId,
            DimensionObjectBinder binder,
            int index = -1,
            int targetVariation = 0,
            int number = 0,
            int entryIndex = -1)
        {
            string switchedOff = binder.ExplainIfSwitchedOff(
                "'" + ownerObjectName + "' names its " + DimensionObjectLinkWords.For(link) + " as",
                targetId);
            if (switchedOff != null)
            {
                UnityEngine.Debug.LogWarning("[ExpandNullforge] " + switchedOff);
                return;
            }

            if (!binder.IsDeferred(targetId))
            {
                return;
            }

            builder.AppendLine(
                "    ExpandNullforge.Foundation.DimensionObjectLinkRegistry.Register(");
            builder.Append("        ").Append(ToCSharpString(ownerObjectName)).AppendLine(",");
            builder.Append("        ExpandNullforge.Foundation.DimensionObjectLink.")
                .Append(link.ToString()).AppendLine(",");
            builder.Append("        ")
                .Append(ToCSharpString(DimensionObjectNamespace.Qualify(modName, targetId)))
                .AppendLine(",");
            builder.Append("        ").Append(index).AppendLine(",");
            builder.Append("        ").Append(targetVariation).AppendLine(",");
            builder.AppendLine("        0,");
            builder.Append("        ").Append(entryIndex).AppendLine(",");
            builder.Append("        ").Append(number).AppendLine(");");
        }
    }
}
