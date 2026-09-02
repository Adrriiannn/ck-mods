using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// How hard a creature hits, how far it sees, and what each ability writes.
    /// </summary>
    internal static partial class DimensionCreatureGenerator
    {
        /// <summary>
        /// Writes the creature's health, and stops the game recomputing it behind the author's back.
        /// </summary>
        /// <remarks>
        /// <c>dontCalculateHealthFromLevel</c> is the whole point of this method. Left false, Core
        /// Keeper recalculates <c>maxHealth</c> from the area level every time the prefab is validated
        /// — so the number the author typed would survive until the first time anyone touched the
        /// asset, and then quietly become something else.
        /// </remarks>
        private static void ApplyHealth(GameObject root, Request request)
        {
            HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
            DimensionCreatureStatsTemplate stats = request.Stats;

            health.dontCalculateHealthFromLevel = stats.UsesAuthoredNumbers;

            if (stats.UsesAuthoredNumbers)
            {
                health.maxHealth = stats.MaxHealth;
                health.maxHealthMultiplier = 1f;
            }

            health.overrideStartHealth = stats.HasStartHealthOverride;
            health.normalizedOverrideStartHealth = stats.StartHealthFraction;
            health.startHealth = Mathf.RoundToInt(health.maxHealth * stats.StartHealthFraction);
        }

        private static void ApplyDamageReduction(GameObject root, Request request)
        {
            DimensionCreatureStatsTemplate stats = request.Stats;

            // A creature with no reduction and no caps wants no component at all — an empty one is a
            // row the game reads every hit to learn that nothing happens.
            bool wanted = stats.DamageReduction > 0 ||
                stats.MaxDamagePerHit > 0 ||
                stats.MinDamagePerHit > 0 ||
                !stats.UsesAuthoredNumbers;

            if (!wanted)
            {
                RemoveComponentIfPresent<DamageReductionAuthoring>(root);
                return;
            }

            DamageReductionAuthoring reduction = EnsureComponent<DamageReductionAuthoring>(root);
            reduction.calculateReductionFromLevel = !stats.UsesAuthoredNumbers;
            reduction.reduction = stats.DamageReduction;
            reduction.reductionMultiplier = 1f;
            reduction.maxDamagePerHit = stats.MaxDamagePerHit;
            reduction.minDamagePerHit = stats.MinDamagePerHit;
        }

        private static void ApplyMovement(GameObject root, Request request)
        {
            MovementSpeedAuthoring movement = EnsureComponent<MovementSpeedAuthoring>(root);
            movement.speed = request.Stats.MoveSpeed;
        }

        /// <summary>
        /// Applies only the chase values the author actually set.
        /// </summary>
        /// <remarks>
        /// Left-alone fields matter here. A borrowed behaviour ships with chase distances tuned to how
        /// its attack works, and writing zero over them would produce a creature that runs into the
        /// player and never swings. So an unset field keeps the behaviour's own value.
        /// </remarks>
        private static void ApplyChase(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            DimensionCreatureStatsTemplate stats = request.Stats;

            // An untouched pursuit section must not add a chase, and must not overwrite one a
            // behaviour brought with it. See DimensionPursuitTemplate.HasAnySetting.
            DimensionPursuitTemplate pursuit =
                request.Combat != null ? request.Combat.Pursuit : null;
            bool authoredPursuit = pursuit != null && pursuit.HasAnySetting;

            if (!stats.HasChaseAtDistance && !stats.HasChaseSpeedMultiplier && !authoredPursuit)
            {
                return;
            }

            ChaseStateAuthoring chase = EnsureComponent<ChaseStateAuthoring>(root);
            if (stats.HasChaseAtDistance)
            {
                chase.chaseAtDistance = stats.ChaseAtDistance;
            }

            if (stats.HasChaseSpeedMultiplier)
            {
                chase.moveSpeedMultiplier = stats.ChaseSpeedMultiplier;
            }

            if (!authoredPursuit)
            {
                return;
            }

            DimensionObjectSpine.ApplyPursuit(root, pursuit);

            if (pursuit.StandoffRangeIsBackwards)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' hangs back further than it will ever close in, " +
                    "so its standoff range is inside out. The two have been read as the same " +
                    "distance, which makes it stand still at that range instead.");
            }

            if (pursuit.WillFollowForeverWithoutArriving)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' needs a path before it will chase and also never " +
                    "gives up once it starts. A target that becomes unreachable will be followed " +
                    "forever by something that cannot arrive.");
            }
        }

        /// <summary>
        /// Gives the creature the state machine every vanilla creature has.
        /// </summary>
        /// <remarks>
        /// <c>StateAuthoring</c> is the root the other states hang off, and <c>IdleState</c> and
        /// <c>TookDamageState</c> sit beside it on 1,300+ vanilla prefabs — chests included. Without
        /// them a creature never returns to idle after acting and never reacts visibly to being hit.
        /// The generator wrote only <c>DeathState</c> before this.
        /// </remarks>
        private static void ApplyStateMachine(GameObject root, Request request)
        {
            DimensionObjectSpine.ApplyDamageableStates(root);

            // What every vanilla creature carries and ours did not. The area level is CONDITIONAL:
            // the attack components overwrite their damage from it in OnValidate with no opt-out, so
            // giving one to a creature whose author asked for exact numbers would make authored
            // attack damage impossible to keep.
            DimensionObjectSpine.ApplyUniversal(root, false);
            DimensionObjectSpine.ApplyAreaLevel(root, !request.Stats.UsesAuthoredNumbers);
        }

        /// <summary>
        /// How the creature perceives things and decides what is an enemy.
        /// </summary>
        /// <remarks>
        /// Chasing is downstream of noticing. <c>ChaseStateAuthoring</c> on its own has nothing to
        /// chase — <c>NearbyEntitiesTrackerAuthoring</c> is what populates the candidates, and
        /// <c>FactionAuthoring</c> plus <c>BehaviourTagsAuthoring</c> decide which of them count.
        /// Every vanilla enemy carries all three.
        /// </remarks>
        private static void ApplyPerception(
            GameObject root,
            DimensionCreatureCombatTemplate combat,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            // A LAYER MASK OF ZERO NOTICES NOTHING, EVER. NearbyEntitiesTrackerSystem hands the
            // number straight to the overlap as CollidesWith, so zero means the list of nearby
            // things stays empty for the creature's whole life — and everything that picks a target
            // off that list (melee, ranged, ray, mortar, eating, healing allies, placing objects,
            // and the chase's candidate list) finds nobody. The field's default is 0 and its label
            // says that leaves the game's own choice, so this is where that becomes true: the
            // game's own creatures watch one layer, and LarvaEntity is where the number comes from.
            uint watches = combat.NoticesThingsOnLayers > 0
                ? (uint)combat.NoticesThingsOnLayers
                : DimensionQueryCompanions.VanillaCreatureNoticeLayers;

            // A REACH OF ZERO IS THE SAME FAILURE AS A MASK OF ZERO, and it is allowed by the
            // field. An overlap of no size returns nothing whatever it is watching, so a creature
            // authored at zero notices nothing for its whole life — and the framework's own
            // "cannot see far enough to shoot" warning is itself gated on the reach being above
            // zero, so it says nothing either. The game's own creature reaches eight tiles.
            float reach = combat.DetectionRadius > 0f
                ? combat.DetectionRadius
                : DimensionQueryCompanions.VanillaCreatureNoticeRadius;

            if (combat.DetectionRadius <= 0f)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' was set to notice things no distance away, " +
                    "which means it notices nothing at all — not a player to chase, not a meal to " +
                    "eat, not an ally to heal. It was generated noticing things " +
                    DimensionQueryCompanions.VanillaCreatureNoticeRadius +
                    " tiles away, which is what the game's own creatures do.");
            }

            DimensionQueryCompanions.SeesNearbyThings(
                root,
                reach,
                watches,
                combat.ChecksForThingsEveryFrame,
                delegate(string message)
                {
                    report.Warnings.Add("'" + request.DisplayName + "' " + message);
                });

            CombatRadiusAuthoring combatRadius = EnsureComponent<CombatRadiusAuthoring>(root);
            combatRadius.radius = combat.CombatRadius;

            // Conditions are how Core Keeper does poison, slow, burning and every buff — a creature
            // without this component simply ignores all of them, which reads as immunity nobody asked
            // for.
            EnsureComponent<SupportsConditionsAuthoring>(root);

            FactionID faction;
            if (!string.IsNullOrEmpty(combat.FactionId) &&
                Enum.TryParse(combat.FactionId, false, out faction))
            {
                EnsureComponent<FactionAuthoring>(root).faction = faction;
            }
            else if (!string.IsNullOrEmpty(combat.FactionId))
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' names faction '" + combat.FactionId +
                    "', which the game does not have. It will have no faction, so nothing will treat " +
                    "it as an enemy.");
            }

            string[] wants = combat.WantsToAttackTags;
            string[] cant = combat.CantAttackTags;

            // THE COMPONENT STAYS ON even when the temper is Custom and both lists are empty.
            // Taking it off quietly switches the whole creature off. Every attack, the chase,
            // eating and exploding are gated on the tag component being PRESENT —
            // ChaseStateRequest, MeleeAttackStateRequest, RangeAttackStateRequest,
            // ChargeAttackStateRequest, JumpAttackStateRequest, EatStateRequest and
            // ExplodeStateRequest all ask for it before they look at anything else, and six systems
            // name it in their work query. Empty lists are a real answer ("nothing here is my
            // enemy"); no component at all is not an answer, it is the creature leaving the game.
            // Clearing the lists is what makes generation authoritative, and that still happens.

            BehaviourTagsAuthoring tags = DimensionQueryCompanions.DecidesWhoIsAnEnemy(root);
            tags.wantsToAttackTags = ParseTags(wants, request, report);
            tags.cantAttackTags = ParseTags(cant, request, report);

            // The third list, and it has a field behind it. Forcing it empty here makes
            // BehaviourTagsCD.Eats false for every creature the framework makes — so the
            // eat, breed and evolve answers all generate cleanly and none of them can fire.
            tags.eatsTags = ParseTags(combat.EatsTags, request, report);
        }

        /// <summary>
        /// What it does when nothing is happening.
        /// </summary>
        /// <remarks>
        /// <c>RandomWalkState</c>, not <c>RoamingState</c> — measured across the game's 115 enemy
        /// prefabs, random walk is what they wander with and roaming state is on four prefabs total.
        /// Removed rather than left behind when the answer turns to "stand still", or the creature
        /// keeps wandering with nothing in the asset still asking it to.
        /// </remarks>
        private static void ApplyIdleMovement(GameObject root, DimensionCreatureCombatTemplate combat)
        {
            if (!combat.WandersWhenIdle)
            {
                RemoveComponentIfPresent<RandomWalkStateAuthoring>(root);
                return;
            }

            RandomWalkStateAuthoring walk = EnsureComponent<RandomWalkStateAuthoring>(root);
            walk.minWalkDistance = combat.MinWanderDistance;
            walk.maxWalkDistance = combat.MaxWanderDistance;
            walk.minIdleDuration = combat.MinWanderPause;
            walk.maxIdleDuration = combat.MaxWanderPause;
            walk.movementSpeedMultiplier = combat.WanderSpeedMultiplier;
            walk.maxWalkDuration = combat.MaxWanderDuration;
            walk.walkPatternBehaviourDefinition = combat.WalkPattern;
            walk.overrideUseAuthoringBehaviourValuesForAllPatternBaseMovementProperties =
                combat.OwnNumbersBeatTheWalkPattern;
        }

        /// <summary>
        /// The attack itself — the piece that was missing entirely.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Before this, a generated creature got <c>ChaseStateAuthoring</c> and nothing else: it ran
        /// at the player and then stood there, because Core Keeper puts the attack on a separate
        /// component and neither <c>MeleeAttackStateAuthoring</c> nor <c>RangeAttackStateAuthoring</c>
        /// was being written.
        /// </para>
        /// <para>
        /// Damage left at zero is deliberately NOT written, so the game's own level scaling stays in
        /// charge — <c>MeleeAttackStateAuthoring.OnValidate</c> recomputes <c>meleeDamage</c> from
        /// <c>AreaLevelAuthoring</c> whenever one is present, so writing a number there would be
        /// overwritten anyway on the next validate and is worse than leaving it alone.
        /// </para>
        /// </remarks>
        private static void ApplyAttacks(
            GameObject root,
            DimensionCreatureCombatTemplate combat,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            if (combat.HasMelee)
            {
                // Melee needs to know it actually touched something.
                EnsureComponent<DetectCollisionAuthoring>(root);

                MeleeAttackStateAuthoring melee = EnsureComponent<MeleeAttackStateAuthoring>(root);
                melee.anticipationDuration = combat.MeleeWindUp;
                melee.hitDuration = combat.MeleeSwingDuration;
                melee.minCooldown = combat.MeleeMinCooldown;
                melee.maxCooldown = combat.MeleeMaxCooldown;
                melee.minDistanceToAttemptHit = combat.MeleeReach;
                melee.hitDistanceInfront = combat.MeleeSwingLandsAhead;
                melee.hitRadius = combat.MeleeHitRadius;
                melee.amountOfHits = combat.MeleeHits;
                melee.pushForce = combat.MeleePushForce;
                melee.hitTiles = combat.MeleeBreaksTiles;
                melee.tileDamage = combat.MeleeTileDamage;

                if (!combat.MeleeDamageFromLevel)
                {
                    melee.meleeDamage = combat.MeleeDamage;
                }

                // The half of the swing that decides how it feels — and the multipliers, which are
                // the only melee damage dial that survives a tier at all.
                DimensionObjectSpine.ApplyMeleeShape(
                    root,
                    combat.MeleeShape,
                    delegate(string objectId) { return ResolveObject(objectId); },
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                if (combat.MeleeDamageFromLevel && combat.MeleeShape.MultipliedDownToNoDamage)
                {
                    report.Warnings.Add(
                        "'" + request.DisplayName + "' takes its melee damage from its tier and " +
                        "then multiplies it by zero, so it swings and hurts nothing. Its damage " +
                        "number is not read on a tiered creature; the multiplier is.");
                }
            }
            else
            {
                RemoveComponentIfPresent<MeleeAttackStateAuthoring>(root);
            }

            if (!combat.HasRanged)
            {
                RemoveComponentIfPresent<RangeAttackStateAuthoring>(root);
                return;
            }

            RangeAttackStateAuthoring ranged = EnsureComponent<RangeAttackStateAuthoring>(root);
            ranged.anticipationDuration = combat.RangedWindUp;
            ranged.minCooldown = combat.RangedMinCooldown;
            ranged.maxCooldown = combat.RangedMaxCooldown;
            ranged.minDistanceFromTargetToAllowAttack = combat.RangedMinDistance;
            ranged.maxDistanceFromTargetToAllowAttack = combat.RangedMaxDistance;
            ranged.projectilesPerShot = combat.ProjectilesPerShot;
            ranged.spreadAngle = combat.SpreadAngle;
            ranged.timeBetweenShots = combat.TimeBetweenShots;

            if (!combat.RangedDamageFromLevel)
            {
                ranged.rangeDamage = combat.RangedDamage;
            }

            // The shot pattern, and the multiplier that is the only ranged damage a tier keeps.
            DimensionObjectSpine.ApplyRangedShape(
                root,
                combat.RangedShape,
                delegate(string message)
                {
                    report.Warnings.Add("'" + request.DisplayName + "' " + message);
                });

            if (combat.RangedDamageFromLevel && combat.RangedShape.MultipliedDownToNoDamage)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' takes its ranged damage from its tier and then " +
                    "multiplies it by zero, so it fires and hurts nothing. Its damage number is not " +
                    "read on a tiered creature; the multiplier is.");
            }

            ObjectID projectile = ResolveObject(combat.ProjectileItemId);

            // WRITTEN EVERY TIME, the None included. For one of the mod's own projectiles the None
            // is the placeholder the link hydration overwrites at load — a creature's shot is
            // RangeAttackStateCD.projectileID, which is a different component from a bow's, and
            // this is the field behind DimensionObjectLink.CreatureShot. Writing it also stops a
            // shot changed to a typo from quietly staying the old projectile, because generation
            // reloads the existing prefab.
            ranged.projectileID = projectile;
            if (projectile == ObjectID.None &&
                !IsDeferred(combat.ProjectileItemId) &&
                !string.IsNullOrEmpty(combat.ProjectileItemId))
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' shoots '" + combat.ProjectileItemId +
                    "', which is neither one of this mod's projectiles nor one the game has. It " +
                    "will go through the motions of shooting and produce nothing.");
            }
        }

        /// <summary>
        /// Writes each ability onto the component that implements it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every kind is removed first and then re-added only if the author asked for it, so dropping
        /// an ability from the list actually takes it off the creature. Left as add-only, a boss would
        /// accumulate every ability it had ever been given across edits, with nothing in the asset
        /// still describing them.
        /// </para>
        /// <para>
        /// Powers left at zero are not written, for the same reason the main attacks leave them alone:
        /// these components recompute damage from <c>AreaLevelAuthoring</c> in <c>OnValidate</c>.
        /// </para>
        /// </remarks>
        private static void ApplyAbilities(
            GameObject root,
            DimensionCreatureCombatTemplate combat,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            RemoveComponentIfPresent<ChargeAttackStateAuthoring>(root);
            RemoveComponentIfPresent<JumpAttackStateAuthoring>(root);
            RemoveComponentIfPresent<ShootMortarProjectileStateAuthoring>(root);
            RemoveComponentIfPresent<ExplodeStateAuthoring>(root);
            RemoveComponentIfPresent<TeleportStateAuthoring>(root);
            RemoveComponentIfPresent<EnrageStateAuthoring>(root);
            RemoveComponentIfPresent<SleepStateAuthoring>(root);
            RemoveComponentIfPresent<EatStateAuthoring>(root);
            RemoveComponentIfPresent<BreedStateAuthoring>(root);
            RemoveComponentIfPresent<EvolveStateAuthoring>(root);
            RemoveComponentIfPresent<HealOtherEntityStateAuthoring>(root);
            RemoveComponentIfPresent<VulnerableStateAuthoring>(root);

            DimensionCreatureAbility[] abilities = combat.Abilities;
            for (int i = 0; i < abilities.Length; i++)
            {
                ApplyAbility(root, abilities[i], request, report);
            }

            if (combat.HasDuplicateAbilities)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' lists the same ability more than once. Each one is " +
                    "a single component, so the later entry overwrites the earlier and its settings " +
                    "are lost.");
            }
        }

        private static void ApplyAbility(
            GameObject root,
            DimensionCreatureAbility ability,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            if (ability.IsMissingTargetObject)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' has a " + ability.Kind +
                    " ability with nothing named for it to use, so it will run and produce nothing.");
            }

            ObjectID target = ResolveObject(ability.TargetObjectId);
            if (target == ObjectID.None && !string.IsNullOrEmpty(ability.TargetObjectId))
            {
                // Two different problems, and telling a creator the wrong one costs them an hour.
                // An ability's target is the one reference on a creature the framework still cannot
                // hand over to the runtime, so one of the mod's own objects here is a real limit
                // rather than a mistake, and it is said as one.
                report.Warnings.Add(IsDeferred(ability.TargetObjectId)
                    ? "'" + request.DisplayName + "' has a " + ability.Kind + " ability naming '" +
                        ability.TargetObjectId + "', which is one of your own. An ability cannot " +
                        "point at your own objects yet — only at the game's. Name one of the " +
                        "game's, or give the creature this behaviour through its own attack instead."
                    : "'" + request.DisplayName + "' has a " + ability.Kind + " ability naming '" +
                        ability.TargetObjectId + "', which is neither one of this mod's objects " +
                        "nor one the game has, so that ability produces nothing.");
            }

            switch (ability.Kind)
            {
                case DimensionCreatureAbilityKind.ChargeAttack:
                {
                    ChargeAttackStateAuthoring charge = EnsureComponent<ChargeAttackStateAuthoring>(root);
                    charge.anticipationDuration = ability.WindUp;
                    charge.chargeDuration = ability.Duration;
                    charge.minCooldown = ability.MinCooldown;
                    charge.maxCooldown = ability.MaxCooldown;
                    charge.distanceToProvokeCharge = ability.Range;
                    charge.moveSpeedMultiplier = ability.SpeedMultiplier;

                    if (!ability.PowerFromLevel)
                    {
                        charge.damage = ability.Power;
                    }

                    DimensionObjectSpine.ApplyChargeShape(
                        root,
                        ability.ChargeShape,
                        delegate(string message)
                        {
                            report.Warnings.Add("'" + request.DisplayName + "' " + message);
                        });
                    break;
                }

                case DimensionCreatureAbilityKind.JumpAttack:
                {
                    JumpAttackStateAuthoring jump = EnsureComponent<JumpAttackStateAuthoring>(root);
                    jump.anticipationTime = ability.WindUp;
                    jump.airTime = ability.Duration;
                    jump.minCooldown = ability.MinCooldown;
                    jump.maxCooldown = ability.MaxCooldown;
                    jump.distanceToAttack = ability.Range;
                    jump.jumpMoveSpeed = ability.SpeedMultiplier;
                    jump.jumpDamageMultiplier = ability.LeapHitsThisHardForItsTier;
                    jump.canOnlyAttackEnemiesAndPlayer = ability.LeapOnlyHitsEnemiesAndPlayers;
                    if (!ability.PowerFromLevel)
                    {
                        jump.jumpDamage = ability.Power;
                    }

                    break;
                }

                case DimensionCreatureAbilityKind.MortarShot:
                {
                    ShootMortarProjectileStateAuthoring mortar =
                        EnsureComponent<ShootMortarProjectileStateAuthoring>(root);
                    mortar.anticipationDuration = ability.WindUp;
                    mortar.attackDuration = ability.Duration;
                    mortar.minCooldown = ability.MinCooldown;
                    mortar.maxCooldown = ability.MaxCooldown;
                    mortar.maxDistanceToTargetToShoot = ability.Range;
                    if (target != ObjectID.None)
                    {
                        mortar.mortarProjectileID = target;
                    }

                    if (!ability.PowerFromLevel)
                    {
                        mortar.mortarDamage = ability.Power;
                    }

                    DimensionObjectSpine.ApplyMortarBarrage(
                        root,
                        ability.MortarBarrage,
                        delegate(string message)
                        {
                            report.Warnings.Add("'" + request.DisplayName + "' " + message);
                        });
                    break;
                }

                case DimensionCreatureAbilityKind.Explode:
                {
                    ExplodeStateAuthoring explode = EnsureComponent<ExplodeStateAuthoring>(root);
                    explode.explodeDuration = ability.Duration;
                    explode.distanceToExplode = ability.Range;
                    explode.minHealthRatioToExplode = ability.HealthFraction;
                    explode.explodeOnDeath = ability.OnDeath;

                    // Vanilla's own default is true, which makes anything carrying this component blow
                    // up the moment it spawns. That is right for a thrown bomb and catastrophic for a
                    // creature, so it is off unless the ability is explicitly a death rattle.
                    explode.explodeOnInitialization = false;
                    if (!ability.PowerFromLevel)
                    {
                        explode.damage = ability.Power;
                    }

                    // ONLY THE EXPLOSION OBJECT DEPENDS ON HAVING ONE. The five settings below
                    // were inside this branch, which meant a creature that blew up without naming
                    // an explosion silently lost its blast variation, its terrain damage, both of
                    // its multipliers and whether it drops its loot — five controls a creator can
                    // see and fill in, thrown away for an unrelated reason. They describe the
                    // blast itself and are written whether or not an explosion object is named.
                    if (target != ObjectID.None)
                    {
                        explode.explosionID = target;
                    }

                    explode.explosionVariation = ability.ExplosionVariation;
                    explode.dropLootOnDestroy = ability.DropsItsLootWhenItBlowsUp;
                    explode.tileDamage = ability.FlatTerrainDamage;
                    explode.damageMultiplier = ability.HitsThisHardForItsTier;
                    explode.tileDamageMultiplier = ability.BreaksTerrainThisHardForItsTier;

                    break;
                }

                case DimensionCreatureAbilityKind.Teleport:
                {
                    TeleportStateAuthoring teleport = EnsureComponent<TeleportStateAuthoring>(root);
                    teleport.startTeleportDuration = ability.WindUp;
                    teleport.endTeleportDuration = ability.Duration;
                    teleport.minCooldown = ability.MinCooldown;
                    teleport.maxCooldown = ability.MaxCooldown;
                    teleport.maxTeleportDistanceFromPlayer = ability.Range;
                    teleport.canOnlyTeleportToNonBlockedGround = true;
                    teleport.canOnlyTeleportBackToSpawn = ability.OnlyGoesBackToWhereItSpawned;
                    teleport.canTeleportToPitAndWater = ability.CanLandOnPitsAndWater;
                    teleport.allowedRadiusToMoveFromPosition =
                        ability.StaysWithinThisFarOfWhereItWas;
                    teleport.minTeleportDistanceFromPlayer =
                        ability.NeverLandsCloserToThePlayerThan;
                    teleport.updateTilesAtAreaMinCorner = new Unity.Mathematics.int2(
                        ability.RefreshesTilesFromCorner.x,
                        ability.RefreshesTilesFromCorner.y);
                    teleport.updateTilesAtAreaMaxCorner = new Unity.Mathematics.int2(
                        ability.RefreshesTilesToCorner.x,
                        ability.RefreshesTilesToCorner.y);
                    break;
                }

                case DimensionCreatureAbilityKind.Enrage:
                {
                    EnrageStateAuthoring enrage = EnsureComponent<EnrageStateAuthoring>(root);
                    enrage.enrageAtHealthRatio = ability.HealthFraction;
                    enrage.duration = ability.Duration;
                    enrage.leaveEnrageAtHealthRatio = ability.CalmsDownAboveHealth;
                    break;
                }

                case DimensionCreatureAbilityKind.Sleep:
                {
                    SleepStateAuthoring sleep = EnsureComponent<SleepStateAuthoring>(root);
                    sleep.minPreFallAsleepDuration = ability.WindUp;
                    sleep.maxPreFallAsleepDuration = ability.WindUp;
                    sleep.minSleepDuration = ability.Duration;
                    sleep.maxSleepDuration = ability.Duration;
                    sleep.minSleepCooldown = ability.MinCooldown;
                    sleep.maxSleepCooldown = ability.MaxCooldown;
                    sleep.radiusFromVisiblePlayerToAwake = ability.Range;
                    sleep.wakeUpDuration = ability.WakeUpSeconds;
                    sleep.minRadiusFromOwnerToWakeUp = ability.WakesWhenItsOwnerIsWithin;
                    sleep.stayAwakeUntilNoVisiblePlayer = ability.StaysAwakeWhileSeen;
                    sleep.triggerAwakeOnClientWhenDamagingEntity =
                        ability.WakesTheMomentItHitsSomething;
                    break;
                }

                case DimensionCreatureAbilityKind.Eat:
                {
                    EatStateAuthoring eat = EnsureComponent<EatStateAuthoring>(root);
                    eat.duration = ability.Duration;
                    eat.distanceToEat = ability.Range;
                    eat.maxFoodUntilFull = ability.Amount;
                    eat.eatPostDuration = ability.PauseAfterEating;
                    break;
                }

                case DimensionCreatureAbilityKind.Breed:
                {
                    BreedStateAuthoring breed = EnsureComponent<BreedStateAuthoring>(root);
                    breed.mealsToTrigger = ability.Amount;
                    breed.minDistanceToBreed = ability.Range;
                    if (target != ObjectID.None)
                    {
                        breed.babyType = target;
                    }

                    // OUTSIDE THE BABY-TYPE BRANCH, and it must stay outside. A mutation is a
                    // VARIATION of whatever the baby is — BreedStateConverter reads
                    // mutationChance and the weights whether or not babyType is set, and an
                    // unset babyType means the young are the same object as the parent, which is
                    // the ordinary case for an animal that breeds true. Inside the branch, an
                    // author who fills in mutation weights and leaves the baby type blank gets a
                    // breeding animal with every mutation silently dropped.
                    breed.mutationChance = ability.MutationChance;
                    breed.mutationWeights =
                        new System.Collections.Generic.List<BreedStateAuthoring.VariationWithWeight>();
                    DimensionMutationWeight[] mutations = ability.Mutations;
                    for (int m = 0; m < mutations.Length; m++)
                    {
                        breed.mutationWeights.Add(new BreedStateAuthoring.VariationWithWeight
                        {
                            variation = mutations[m].Variation,
                            weight = mutations[m].Weight
                        });
                    }

                    if (ability.MutatesIntoNothing)
                    {
                        report.Warnings.Add(
                            "'" + request.DisplayName + "' can produce mutated young with no " +
                            "list of what they mutate into, so every baby comes out the same.");
                    }

                    break;
                }

                case DimensionCreatureAbilityKind.Evolve:
                {
                    EvolveStateAuthoring evolve = EnsureComponent<EvolveStateAuthoring>(root);
                    evolve.foodAmountToEvolve = ability.Amount;
                    if (target != ObjectID.None)
                    {
                        evolve.toEvolveInto = target;
                    }

                    // A count of meals to evolve after. The game decides whether a creature is
                    // ready to grow up by reading how many meals it remembers, and the evolve
                    // answer does not bring that record with it — so a creature told to evolve
                    // after three meals, and not separately told to remember its meals, was never
                    // considered for evolving at all. Both of the game's baby animals carry it.
                    if (root.GetComponent<MealsEatenAuthoring>() == null)
                    {
                        EnsureComponent<MealsEatenAuthoring>(root);
                        report.Warnings.Add(
                            "'" + request.DisplayName + "' grows up after a number of meals but " +
                            "was not set to remember its meals, and the game counts them off that " +
                            "record. It was generated remembering them, which is what the game's " +
                            "own young animals do.");
                    }

                    break;
                }

                case DimensionCreatureAbilityKind.HealAllies:
                {
                    HealOtherEntityStateAuthoring heal = EnsureComponent<HealOtherEntityStateAuthoring>(root);
                    heal.anticipationDuration = ability.WindUp;
                    heal.healDuration = ability.Duration;
                    heal.minCooldown = ability.MinCooldown;
                    heal.maxCooldown = ability.MaxCooldown;
                    heal.maxReachDistance = ability.Range;
                    if (!ability.PowerFromLevel)
                    {
                        heal.healPerSecond = ability.Power;
                        heal.donCalculateHealingFromLevel = true;
                    }

                    // OUTSIDE THE FIXED-POWER BRANCH, and all four belong outside it. Read off
                    // HealOtherEntityStateConverter: healMultiplier is the field the LEVEL path
                    // uses (LevelToHealing(level, multiplier)), so it is the one field that can
                    // only ever matter to a healer scaled by its tier — inside the branch it is
                    // written only for one that is not. The other three — heals a share of max
                    // health, keeps going until hit so many times, heals what it cannot see — are
                    // copied straight into HealOtherEntityStateCD whichever way the power is
                    // worked out.
                    heal.healMultiplier = ability.HealsThisMuchForItsTier;
                    heal.healPercentageOfHp = ability.HealsAShareOfMaxHealth;
                    heal.keepHealingUntilTakingDamageXTimes =
                        ability.KeepsHealingUntilHitThisManyTimes;
                    heal.skipVisibilityCheck = ability.HealsWhatItCannotSee;

                    break;
                }

                case DimensionCreatureAbilityKind.Vulnerable:
                {
                    VulnerableStateAuthoring vulnerable = EnsureComponent<VulnerableStateAuthoring>(root);
                    vulnerable.anticipationDuration = ability.WindUp;
                    vulnerable.vulnerableDuration = ability.Duration;
                    vulnerable.preAnticipationDuration = ability.PreWindUp;
                    vulnerable.endDuration = ability.RecoveryAfter;
                    vulnerable.destroyTilesWithinRadius = ability.BreaksTerrainWithin;
                    vulnerable.pushBackNearbyEntitiesForce = ability.ShovesNearbyWithForce;
                    vulnerable.pushBackNearbyEntitiesForceRadius = ability.ShoveReaches;
                    vulnerable.maxHealthRatioLostToLeaveState =
                        ability.LeavesAfterLosingThisMuchHealth;
                    break;
                }
            }
        }
    }
}
