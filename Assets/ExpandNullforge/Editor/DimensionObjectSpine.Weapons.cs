using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The spine a weapon carries: its swing, its reach and what the hit feels like.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        /// <summary>
        /// Makes a placed thing hurt whatever comes near it.
        /// </summary>
        /// <remarks>
        /// The growing-hit-radius fields are deliberately left at their defaults: three fields with
        /// a whole sub-system behind them and no user anywhere in the game, so writing them would be
        /// shipping a knob nobody has ever turned.
        /// </remarks>
        public static void ApplyContinuousAttack(
            GameObject root,
            DimensionContinuousAttackTemplate attack,
            DimensionWiringTemplate wiring,
            System.Action<string> report)
        {
            if (attack == null || !attack.HurtsWhatComesNear)
            {
                RemoveComponentIfPresent<AttackContinuouslyAuthoring>(root);
                return;
            }

            if (attack.AttacksHarmlessly && report != null)
            {
                report("hurts what comes near it for no damage at all.");
            }

            if (attack.ReachesNothing && report != null)
            {
                report(
                    "hurts what comes near it but reaches nothing, so nothing ever comes near " +
                    "enough. Most of the game uses " +
                    DimensionContinuousAttackTemplate.OrdinaryHitRadius + ".");
            }

            if (attack.NeedsPowerButIsNotWired(wiring) && report != null)
            {
                report(
                    "needs power to attack and is not wired for any, so it will never fire. Give " +
                    "it a wiring role, or untick the power requirement.");
            }

            AttackContinuouslyAuthoring authored = EnsureComponent<AttackContinuouslyAuthoring>(root);
            authored.damage = attack.Damage;
            authored.damageMultiplier = attack.DamageMultiplier;
            authored.hitRadius = attack.Reach;
            authored.attackTime = attack.AttackSeconds;
            authored.cooldownAfterHit = attack.RestSeconds;
            authored.pushback = attack.Pushback;
            authored.requiresElectricity = attack.NeedsPower;
            authored.isStatic = attack.StaysPut;
            authored.cantDamageObjectsHangingOnWalls = attack.SparesThingsOnWalls;
            authored.canHitLowTriggers = attack.ReachesLowThings;
            authored.skipLootDropIfDestroyPlants = attack.PlantsItBreaksDropNothing;
            authored.hitRadiusGrowOverTime = attack.ReachGrowsWhileAttacking;
            authored.hitRadiusAfterGrowth = attack.ReachGrowsTo;
            authored.hitRadiusGrowthRate = attack.ReachGrowthRate;
            authored.canDamageOnlyEnemyAndPlayer = attack.OnlyHitsEnemiesAndPlayers;
            authored.canOnlyHitCertainNonEnemyObjects = attack.OnlyHitsCertainNonEnemies;
            authored.ignoreDamageReduction = attack.IgnoresDamageReduction;
            authored.damageEffectType = (DamageEffectType)(int)attack.DamageFlavour;
            authored.breakAfterSuccessfulHit = attack.BreaksAfterALandedHit;
            authored.breakDelay = attack.BreaksAfterThisLong;
            authored.triggerIdleAnimationOnEnteringState = attack.PlaysIdleOnStarting;
            authored.triggerAnimationOnHit = attack.HitAnimation;
        }

        /// <summary>
        /// Makes an item swing, fire or cast.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Core Keeper keeps the three kinds in three separate components and an item carries exactly
        /// one, so the other two are removed rather than left behind. That matters more than it
        /// sounds: an item that was a sword and became a bow would otherwise carry both, and the
        /// game would swing it as well as fire it.
        /// </para>
        /// <para>
        /// The skill multiplier rides along regardless of kind, because it is on every weapon in the
        /// game and is what decides how fast using the thing raises the matching skill.
        /// </para>
        /// </remarks>
        public static void ApplyWeapon(
            GameObject root,
            DimensionWeaponTemplate weapon,
            System.Func<string, ObjectID> resolveProjectile,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (weapon == null || !weapon.IsAWeapon)
            {
                RemoveComponentIfPresent<MeleeWeaponAuthoring>(root);
                RemoveComponentIfPresent<RangeWeaponAuthoring>(root);
                RemoveComponentIfPresent<CastItemAuthoring>(root);
                RemoveComponentIfPresent<WeaponSkillGainedMultiplierAuthoring>(root);
                return;
            }

            if (!weapon.IsMelee)
            {
                RemoveComponentIfPresent<MeleeWeaponAuthoring>(root);
            }

            if (!weapon.IsRanged)
            {
                RemoveComponentIfPresent<RangeWeaponAuthoring>(root);
            }

            if (!weapon.IsCast)
            {
                RemoveComponentIfPresent<CastItemAuthoring>(root);
            }

            EnsureComponent<WeaponSkillGainedMultiplierAuthoring>(root).skillMultiplier =
                weapon.SkillGainMultiplier;

            // A beam is its own weapon class, not a flavour of melee, ranged or cast — so it is
            // applied before the kind branches rather than inside one of them.
            ApplyBeamWeapon(root, weapon.Beam, resolveProjectile, report, isDeferred);

            if (weapon.IsMelee)
            {
                MeleeWeaponAuthoring melee = EnsureComponent<MeleeWeaponAuthoring>(root);
                melee.baseHitColliderSize = weapon.Reach;
                melee.extraHitColliderReachSize = weapon.ExtraReach;
                melee.arcAngle = (ArcAngle)(int)weapon.SwingArc;
                melee.attackFXType = (AttackFXType)(int)weapon.Flourish;
                melee.lungeForce = weapon.Lunge;
                melee.quickHit = weapon.QuickHit;
                melee.isBigSpearWeapon = weapon.ThrustsLikeASpear;
                melee.isBigSwingWeapon = weapon.SwingsLikeATwoHander;
                melee.skipAnticipationAnimation = weapon.SkipsTheWindUpAnimation;
                melee.tileDamageAOE = weapon.BreaksTerrainAcrossTheWholeArc;
                melee.overrideAnimation = weapon.MeleeOverrideAnimation;
                melee.disable = weapon.MeleeDisabled;
                return;
            }

            if (weapon.IsRanged)
            {
                RangeWeaponAuthoring ranged = EnsureComponent<RangeWeaponAuthoring>(root);
                ObjectID projectile = resolveProjectile == null
                    ? ObjectID.None
                    : resolveProjectile(weapon.FiresProjectileId);

                if (projectile == ObjectID.None &&
                    !string.IsNullOrEmpty(weapon.FiresProjectileId) &&
                    !(isDeferred != null && isDeferred(weapon.FiresProjectileId)) &&
                    report != null)
                {
                    report(
                        "fires '" + weapon.FiresProjectileId + "', which is neither a projectile in " +
                        "this mod nor one the game has, so nothing will come out of it.");
                }

                ranged.projectileID = projectile;
                ranged.extraProjectiles = weapon.ExtraShots;
                ranged.spreadAngle = weapon.SpreadAngle;
                ranged.spawnOffsetDistance = weapon.MuzzleDistance;
                ranged.rotateFreely = weapon.AimsFreely;
                ranged.recoilForce = weapon.Recoil;

                ranged.spawnRandomProjectile = weapon.FiresARandomProjectile;
                ranged.randomProjectiles = new System.Collections.Generic.List<ObjectID>();
                string[] randomIds = weapon.RandomProjectileIds;
                for (int i = 0; i < randomIds.Length; i++)
                {
                    ObjectID one = resolveProjectile == null
                        ? ObjectID.None
                        : resolveProjectile(randomIds[i]);
                    if (one != ObjectID.None)
                    {
                        ranged.randomProjectiles.Add(one);
                    }
                    else if (isDeferred != null && isDeferred(randomIds[i]))
                    {
                        // A PLACEHOLDER AT THE RIGHT POSITION, not a dropped entry. The list becomes
                        // RangeWeaponCD.randomProjectiles, a FixedList64Bytes with no room to grow at
                        // load — and dropping this entry would slide every later shot one place left,
                        // so the runtime write would land on the wrong one.
                        ranged.randomProjectiles.Add(ObjectID.None);
                    }
                    else if (report != null)
                    {
                        report(
                            "picks from '" + randomIds[i] + "', which is neither a projectile in " +
                            "this mod nor one the game has, so that one is left out of the list.");
                    }
                }

                ObjectID second =
                    string.IsNullOrEmpty(weapon.SecondProjectileId) || resolveProjectile == null
                        ? ObjectID.None
                        : resolveProjectile(weapon.SecondProjectileId);
                bool secondIsOneOfOurs =
                    second == ObjectID.None &&
                    isDeferred != null &&
                    isDeferred(weapon.SecondProjectileId);

                ranged.secondaryProjectileVariationID = second;

                if (second == ObjectID.None && !secondIsOneOfOurs &&
                    !string.IsNullOrEmpty(weapon.SecondProjectileId) && report != null)
                {
                    // This branch said nothing at all before, so a misspelled wound-up shot
                    // generated clean and the full charge fired the ordinary shot instead.
                    report(
                        "fires '" + weapon.SecondProjectileId + "' once its wind-up is full, which " +
                        "is neither a projectile in this mod nor one the game has, so a full " +
                        "wind-up fires its ordinary shot instead.");
                }

                ranged.pierceAtMaxWindup = weapon.PiercesAtFullWindUp;
                ranged.bounceAtMaxWindup = weapon.BouncesAtFullWindUp;
                ranged.overrideAnimation = weapon.OverrideAnimation;

                // THE SIZE WAITS FOR THE SHOT — AND GOES NOWHERE WITHOUT ONE. RangeWeaponConverter
                // logs a red error naming the creator's prefab whenever an explosion size is set
                // and the explosive shot is None (RangeWeaponConverter.cs:14-17). None is exactly
                // what a deferred shot has to bake as, so for one of ours the size is held back
                // here and the bootstrap row carries the authored number, which the link hydration
                // writes back alongside the shot's id on the first ticks.
                //
                // It is also None when the wound-up shot names NOTHING AT ALL, and that case used
                // to ship the size anyway — so a blast size left on a weapon with no wound-up shot
                // put a red error in the player's log about a prefab the author had done nothing
                // wrong to. A size with no shot to carry it does nothing either way; it is dropped
                // and said, rather than kept and logged by the game.
                bool secondNamesNothing = second == ObjectID.None && !secondIsOneOfOurs;
                ranged.explosionSize = secondIsOneOfOurs || secondNamesNothing
                    ? 0
                    : weapon.ExplosionSize;

                // SAID FOR BOTH HALVES OF "names nothing". The condition used to also require the
                // field to be EMPTY, so the misspelling half — a wound-up shot naming something the
                // game does not have — lost its blast number without a word, while the message
                // above talked only about the shot. Both roads end with the size at zero, so both
                // have to say the size went.
                if (secondNamesNothing && weapon.ExplosionSize > 0f && report != null)
                {
                    report(string.IsNullOrEmpty(weapon.SecondProjectileId)
                        ? "has a blast size on its wound-up shot without naming a shot for the " +
                          "wind-up to fire, so there is nothing for the blast to come off. Name " +
                          "the shot, or set the blast size back to zero."
                        : "has a blast size on its wound-up shot, but '" +
                          weapon.SecondProjectileId + "' is not a shot this world has, so the " +
                          "blast size is dropped along with it.");
                }
                ranged.explosionUseWeaponDamage = weapon.ExplosionUsesWeaponDamage;

                ranged.mortarRaycastToTarget = weapon.LobsOntoTheAimPoint;
                ranged.mortarTargetRange = weapon.LobRange;
                ranged.minMaxRandomSpreadDistance = new Unity.Mathematics.float2(
                    weapon.LobScatterNearAndFar.x,
                    weapon.LobScatterNearAndFar.y);
                ranged.secondaryMinMaxRandomSpreadDistance = new Unity.Mathematics.float2(
                    weapon.SecondLobScatterNearAndFar.x,
                    weapon.SecondLobScatterNearAndFar.y);
                ranged.scaleMortarAirTimeWithDistance = weapon.LobHangsLongerWhenFurther;
                ranged.secondaryScaleMortarAirTimeWithDistance =
                    weapon.SecondLobHangsLongerWhenFurther;
                ranged.minMortarAirTimePercentage = weapon.ShortestLobHangShare;
                ranged.secondaryDistanceBetweenHits = weapon.SecondProjectileDistanceBetweenHits;

                if (report != null)
                {
                    if (weapon.RandomProjectilesWillBeIgnored)
                    {
                        report(
                            "lists projectiles to pick from without switching random firing on, so " +
                            "it only ever fires its single projectile.");
                    }

                    if (weapon.RandomProjectilesAreEmpty)
                    {
                        report(
                            "is set to fire a random projectile with nothing in its list to pick " +
                            "from, so nothing will come out of it.");
                    }
                }

                return;
            }

            CastItemAuthoring cast = EnsureComponent<CastItemAuthoring>(root);
            cast.castTime = weapon.CastSeconds;
            cast.useType = (CastItemUseType)(int)weapon.CastPurpose;
            cast.allowHoldToRepeat = weapon.HoldingRepeatsIt;

            AchievementID castAchievement;
            if (!string.IsNullOrEmpty(weapon.CastAchievementId) &&
                System.Enum.TryParse(weapon.CastAchievementId, false, out castAchievement))
            {
                cast.achievement = castAchievement;
            }

            EffectID castEffect;
            if (!string.IsNullOrEmpty(weapon.CastCompleteEffectId) &&
                System.Enum.TryParse(weapon.CastCompleteEffectId, false, out castEffect))
            {
                cast.castCompleteEffect = castEffect;
            }
        }

        /// <summary>
        /// Writes the shape, timing and force of a melee swing onto an existing
        /// <c>MeleeAttackStateAuthoring</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Like <see cref="ApplyPursuit"/> this never adds the component — the attack kind decides
        /// whether there is a swing at all.
        /// </para>
        /// <para>
        /// THE MULTIPLIERS ARE THE POINT. <c>MeleeAttackStateConverter</c> discards the authored
        /// <c>meleeDamage</c> entirely when the object carries a tier and recomputes it as
        /// <c>LevelToDamage(level, meleeDamageMultiplier)</c>. Before this the framework wrote the
        /// flat number and never the multiplier, so a tiered creature's damage was whatever the
        /// curve said and could not be shifted at all.
        /// </para>
        /// </remarks>
        public static void ApplyMeleeShape(
            GameObject root,
            DimensionMeleeShapeTemplate shape,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            if (root == null || shape == null)
            {
                return;
            }

            MeleeAttackStateAuthoring melee = root.GetComponent<MeleeAttackStateAuthoring>();
            if (melee == null)
            {
                return;
            }

            melee.meleeDamageMultiplier = shape.HitsThisHardForItsTier;
            melee.tileDamageMultiplier = shape.BreaksTerrainThisHardForItsTier;

            melee.durationBeforeDamageDeal = shape.DamageLandsAfter;
            melee.moveForceForward = shape.LungeForce;
            melee.alwaysMoveAtFullForceForward = shape.AlwaysLungesAtFullForce;

            melee.lockOrientationDuringHit = shape.CannotTurnDuringTheHit;
            melee.lockOrientationDuringAnticipation = shape.CannotTurnDuringTheWindUp;
            melee.hitInDiscreteDirections = shape.OnlySwingsInFourDirections;

            melee.skipVisibilityCheck = shape.SwingsAtWhatItCannotSee;
            melee.canOnlyAttackEnemiesAndPlayer = shape.OnlyHitsEnemiesAndPlayers;
            melee.attackPlayerTimeout = shape.GivesUpOnAPlayerAfter;
            melee.canHitLowTriggers = shape.HitsLowObstacles;
            melee.bypassMaxDamagePerHit = shape.IgnoresTheDamageCap;

            melee.hitBoxHalfLength = shape.HitboxHalfLength;
            melee.hitBoxHalfWidth = shape.HitboxHalfWidth;
            melee.hitOffset = new Unity.Mathematics.float3(
                shape.HitboxOffset.x,
                shape.HitboxOffset.y,
                shape.HitboxOffset.z);

            melee.objectToSpawnOnHitTiles = string.IsNullOrEmpty(shape.SpawnsOnBrokenTilesId)
                ? ObjectID.None
                : (resolveObject == null ? ObjectID.None : resolveObject(shape.SpawnsOnBrokenTilesId));

            if (!string.IsNullOrEmpty(shape.SpawnsOnBrokenTilesId) &&
                melee.objectToSpawnOnHitTiles == ObjectID.None &&
                report != null)
            {
                report(
                    "leaves '" + shape.SpawnsOnBrokenTilesId + "' where its swing breaks terrain, " +
                    "which the game does not have. Nothing will be left behind.");
            }

            if (shape.HitboxIsHalfSpecified && report != null)
            {
                report(
                    "overrides only one half of its melee hitbox. The two are read together, so a " +
                    "swing with reach and no width — or width and no reach — connects with nothing. " +
                    "Set both, or leave both at zero and let the game work it out.");
            }
        }

        /// <summary>
        /// Writes the shot pattern onto an existing <c>RangeAttackStateAuthoring</c>.
        /// </summary>
        /// <remarks>
        /// Never adds the component — the attack kind decides whether the creature shoots at all.
        /// </remarks>
        /// <summary>
        /// Writes the detail of a charge onto an existing <c>ChargeAttackStateAuthoring</c>.
        /// </summary>
        public static void ApplyChargeShape(
            GameObject root,
            DimensionChargeShapeTemplate shape,
            System.Action<string> report)
        {
            if (root == null || shape == null)
            {
                return;
            }

            ChargeAttackStateAuthoring charge = root.GetComponent<ChargeAttackStateAuthoring>();
            if (charge == null)
            {
                return;
            }

            charge.damageMultiplier = shape.HitsThisHardForItsTier;
            charge.tileDamageMultiplier = shape.BreaksTerrainThisHardForItsTier;
            charge.hitTiles = shape.PloughsThroughTerrain;

            charge.endChargeWithAttack = shape.EndsWithASwing;
            charge.alwaysEndChargeWithAttack = shape.SwingsEvenIfItHitNothing;
            charge.endChargeDistanceToAttemptAttack = shape.SwingsIfWithin;
            charge.endChargeAttackDuration = shape.EndingSwingDuration;
            charge.endChargeMoveForceForward = shape.EndingSwingLunge;
            charge.chargeAttackAnticipationDuration = shape.EndingSwingWindUp;
            charge.endOfChargeAttackHitTiles = shape.EndingSwingBreaksTerrain;

            charge.collideDuration = shape.TimeStuckOnImpact;
            charge.reversePushback = shape.BouncesBackThisHard;
            charge.dontCollideWithObjects = shape.PassesThroughScenery;
            charge.ignoreLowColliders = shape.LowObstaclesDoNotStopIt;
            charge.triggerAnimationIfNotCollided = shape.PlaysImpactEvenOnAMiss;

            charge.steerTowardsTargetDuringCharge = shape.CanSteerMidCharge;
            charge.steerTowardsTargetMinDistance = shape.StopsSteeringWithin;
            charge.steerTowardsTargetMaxAngleDeg = shape.WidestSteerDegrees;
            charge.lockOrientationAtMultiplier = shape.FacingLocksAt;

            charge.pushback = shape.PushesWhatItHits;
            charge.tileDamage = shape.FlatTerrainDamage;

            charge.steerTowardsTargetChargeAttackRotateToTargetData =
                new ChargeAttackRotateToTargetData
                {
                    chargeAttackRotateToTargetType =
                        (ChargeAttackRotateToTargetType)(int)shape.SteerTurn,
                    degreesPerSecond = shape.SteerDegreesPerSecond
                };

            charge.lockOrientationChargeAttackRotateToTargetData =
                new ChargeAttackRotateToTargetData
                {
                    chargeAttackRotateToTargetType =
                        (ChargeAttackRotateToTargetType)(int)shape.LockTurn,
                    degreesPerSecond = shape.LockDegreesPerSecond
                };

            charge.vulnerabilityDuration = shape.VulnerableFor;
            charge.endDuration = shape.RecoveryAfterwards;

            charge.hitBoxHalfLength = shape.HitboxHalfLength;
            charge.hitBoxHalfWidth = shape.HitboxHalfWidth;
            charge.hitDistanceInfront = shape.HitReach;
            charge.hitRadius = shape.HitRadius;
            charge.hitInDiscreteDirections = shape.OnlyChargesInFourDirections;
            charge.hitOffset = new Unity.Mathematics.float3(
                shape.HitboxOffset.x,
                shape.HitboxOffset.y,
                shape.HitboxOffset.z);

            if (report == null)
            {
                return;
            }

            if (shape.EndingSwingSettingsWillBeIgnored)
            {
                report(
                    "sets up an attack at the end of its charge but never turns that attack on, so " +
                    "it just stops when it arrives and none of those numbers are read.");
            }

            if (shape.SteeringSettingsWillBeIgnored)
            {
                report(
                    "changes how its charge steers without letting it steer at all, so the charge " +
                    "still runs in a straight line.");
            }
        }

        public static void ApplySmashesObjects(
            GameObject root,
            DimensionSmashesObjectsTemplate smash,
            System.Action<string> report)
        {
            if (root == null || smash == null || !smash.SmashesWhatIsInItsWay)
            {
                RemoveComponentIfPresent<DamageObjectStateAuthoring>(root);
                return;
            }

            DamageObjectStateAuthoring smasher =
                EnsureComponent<DamageObjectStateAuthoring>(root);
            smasher.maxAllowedDamagesWithoutGoal = smash.GivesUpAfterSwings;
            smasher.anticipationTime = smash.WindUp;
            smasher.hitDuration = smash.SwingSeconds;
            smasher.hitDistanceInfront = smash.Reach;
            smasher.hitRadius = smash.Radius;
            smasher.damage = smash.FlatObjectDamage;
            smasher.damageMultiplier = smash.HitsObjectsThisHardForItsTier;
            smasher.meleeDamage = smash.FlatCreatureDamage;
            smasher.meleeDamageMultiplier = smash.HitsCreaturesThisHardForItsTier;
            smasher.bypassCantAttackBehaviourWhileChasing = smash.SmashesEvenWhileChasing;

            if (report == null)
            {
                return;
            }

            if (smash.NeverGivesUpOnAWall)
            {
                report(
                    "smashes what is in its way and never gives up, so one it cannot path around " +
                    "will stand at the nearest wall swinging for the rest of the session.");
            }

            if (smash.SwingConnectsWithNothing)
            {
                report(
                    "smashes what is in its way with no reach and no width, so its swing connects " +
                    "with nothing.");
            }
        }

        public static void ApplyBeamWeapon(
            GameObject root,
            DimensionBeamWeaponTemplate beam,
            System.Func<string, ObjectID> resolveProjectile,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (root == null || beam == null || !beam.FiresABeam)
            {
                RemoveComponentIfPresent<BeamWeaponAuthoring>(root);
                return;
            }

            BeamWeaponAuthoring weapon = EnsureComponent<BeamWeaponAuthoring>(root);
            weapon.attackDistance = beam.Reaches;
            weapon.expandWhenHeld = beam.GrowsWhileHeld;
            weapon.expandTimeSeconds = beam.GrowsOverSeconds;
            weapon.expandMinDistance = beam.StartsAtReach;
            weapon.isStickyBeam = beam.LatchesOn;
            weapon.onlyDamageAtEndOfBeam = beam.OnlyTheEndHurts;
            weapon.beamVisualFromCenter = beam.DrawnFromTheCentre;
            weapon.extraProjectiles = beam.ExtraBeams;
            weapon.spreadAngle = beam.SpreadDegrees;
            weapon.overrideAnimation = beam.OverrideAnimation;
            weapon.secondaryOverrideAnimation = beam.HeldAnimation;
            weapon.useRangedLoopAnimation = beam.UsesTheLoopingAnimation;
            weapon.spawnOffsetDistance = new Unity.Mathematics.float3(
                beam.StartsAtOffset.x,
                beam.StartsAtOffset.y,
                beam.StartsAtOffset.z);

            weapon.collideFilterPVPOn = new Unity.Physics.Authoring.PhysicsCategoryTags
            {
                Value = (uint)beam.CollidesWithLayers
            };
            weapon.attackFilterPVPOn = new Unity.Physics.Authoring.PhysicsCategoryTags
            {
                Value = (uint)beam.AttacksLayers
            };

            ConditionID cost;
            if (!string.IsNullOrEmpty(beam.ManaCostCondition) &&
                System.Enum.TryParse(beam.ManaCostCondition, false, out cost))
            {
                weapon.manaCostCondition = cost;
            }
            else if (!string.IsNullOrEmpty(beam.ManaCostCondition) && report != null)
            {
                report(
                    "drains '" + beam.ManaCostCondition + "' while its beam runs, which the game " +
                    "does not have, so the beam is free.");
            }

            ConditionID boost;
            if (!string.IsNullOrEmpty(beam.DamageBoostCondition) &&
                System.Enum.TryParse(beam.DamageBoostCondition, false, out boost))
            {
                weapon.damageIncreaseCondition = boost;
            }
            else if (!string.IsNullOrEmpty(beam.DamageBoostCondition) && report != null)
            {
                report(
                    "is boosted by '" + beam.DamageBoostCondition + "', which the game does not " +
                    "have, so nothing boosts it.");
            }

            weapon.secondaryProjectileVariationID =
                string.IsNullOrEmpty(beam.SecondProjectileId) || resolveProjectile == null
                    ? ObjectID.None
                    : resolveProjectile(beam.SecondProjectileId);

            if (report == null)
            {
                return;
            }

            // One of the mod's own shots is fine — BeamWeaponAuthoring stays on the item either way,
            // and the runtime fills BeamWeaponCD.windupProjectileID in. A name that is neither used
            // to go through with no word said at all.
            if (!string.IsNullOrEmpty(beam.SecondProjectileId) &&
                weapon.secondaryProjectileVariationID == ObjectID.None &&
                !(isDeferred != null && isDeferred(beam.SecondProjectileId)))
            {
                report(
                    "fires '" + beam.SecondProjectileId + "' at the end of a held beam, which is " +
                    "neither a projectile in this mod nor one the game has, so holding it fires " +
                    "nothing extra.");
            }

            if (beam.GrowsInstantly)
            {
                report(
                    "grows its beam while held but over no time at all, so it snaps to full reach " +
                    "immediately.");
            }

            if (beam.StartsLongerThanItEnds)
            {
                report(
                    "starts its beam longer than it can ever grow to, so holding it makes the beam " +
                    "shorter.");
            }

            if (beam.ExtraBeamsWouldOverlap)
            {
                report(
                    "fires extra beams with no spread between them, so they all leave along the " +
                    "same line and only one is visible.");
            }
        }

        public static void ApplyMortarBarrage(
            GameObject root,
            DimensionMortarBarrageTemplate barrage,
            System.Action<string> report)
        {
            if (root == null || barrage == null)
            {
                return;
            }

            ShootMortarProjectileStateAuthoring mortar =
                root.GetComponent<ShootMortarProjectileStateAuthoring>();
            if (mortar == null)
            {
                return;
            }

            mortar.minAmountOfProjectiles = barrage.FewestShells;
            mortar.maxAmountOfProjectiles = barrage.MostShells;
            mortar.maxProjectilesShotPerWave = barrage.ShellsPerWave;
            mortar.maxProjectilesShotPerWaveMultiplier = barrage.ShellsPerWaveMultiplier;
            mortar.timeBetweenProjectiles = barrage.TimeBetweenShells;
            mortar.dontAllowOverlappingShots = barrage.NeverOverlapsShots;

            mortar.lineFromShooterToTarget = barrage.LandsInALineTowardsTheTarget;
            mortar.lineBendTowardTarget = barrage.TheLineBendsToFollow;
            mortar.lineLengthMultiplier = barrage.LineLengthMultiplier;
            mortar.minRandomSpreadDistance = barrage.ScatterFrom;
            mortar.maxRandomSpreadDistance = barrage.ScatterTo;
            mortar.shootAtSelf = barrage.ShellsLandOnItself;

            mortar.goUpTime = barrage.RiseTime;
            mortar.airTime = barrage.HangTime;
            mortar.goDownTime = barrage.FallTime;
            mortar.explodeTime = barrage.FuseTime;

            mortar.minDistanceToShoot = barrage.FiresFromAtLeast;
            mortar.maxHealthRatioToShoot = barrage.OnlyFiresBelowHealth;
            mortar.keepShootingUntilTakingDamageXTimes = barrage.KeepsFiringUntilHitThisManyTimes;
            mortar.onlyShootWhenInCombat = barrage.OnlyFiresWhenInCombat;
            mortar.skipVisibilityCheck = barrage.FiresAtWhatItCannotSee;
            mortar.dontInterruptOtherAttackStates = barrage.DoesNotInterruptItsOtherAttacks;

            mortar.damageMultiplier = barrage.HitsThisHardForItsTier;
            mortar.tileDamageMultiplier = barrage.BreaksTerrainThisHardForItsTier;
            mortar.hitTiles = barrage.ShellsBreakTerrain;
            mortar.mortarTileDamage = barrage.FlatTerrainDamage;

            mortar.overrideAnimID = barrage.AnimationName;
            mortar.playAttackFireAnimation = barrage.PlaysTheFiringAnimation;

            if (report == null)
            {
                return;
            }

            if (barrage.LineSettingsWillBeIgnored)
            {
                report(
                    "sets up a line barrage without switching the line on, so its shells scatter " +
                    "at random instead of walking out towards the target.");
            }

            if (barrage.LineHasNoLength)
            {
                report(
                    "fires its barrage in a line with no length, so every shell lands on the same " +
                    "spot. Give the line a length multiplier for it to walk out.");
            }
        }

        public static void ApplyRangedShape(
            GameObject root,
            DimensionRangedShapeTemplate shape,
            System.Action<string> report)
        {
            if (root == null || shape == null)
            {
                return;
            }

            RangeAttackStateAuthoring ranged = root.GetComponent<RangeAttackStateAuthoring>();
            if (ranged == null)
            {
                return;
            }

            ranged.damageMultiplier = shape.ShotsHitThisHardForItsTier;
            ranged.speedMultiplier = shape.ProjectileSpeedMultiplier;

            ranged.spawnAtDistanceInfront = shape.MuzzleDistance;
            ranged.spawnAtDistanceInfrontDeviation = shape.MuzzleDistanceVariation;
            ranged.spawnOffset = new Unity.Mathematics.float3(
                shape.MuzzleOffset.x,
                shape.MuzzleOffset.y,
                shape.MuzzleOffset.z);
            ranged.spawnDirectionType = shape.FiresAtAnyAngle
                ? ProjectileSpawnDirectionType.Free
                : ProjectileSpawnDirectionType.HorizontalAndVertical;

            ranged.aimDegreesMax = shape.AimConeDegrees;
            ranged.allowReAimingWhileShooting = shape.KeepsAimingWhileShooting;
            ranged.dontAllowReAimingDuringAntipation = shape.LocksAimDuringTheWindUp;
            ranged.minExtrapolatedAimDistance = shape.StartsLeadingTargetsAt;
            ranged.maxExtrapolatedAimDistance = shape.StopsLeadingTargetsAt;

            ranged.spreadType = (ProjectileSpreadType)(int)shape.ShotPattern;
            ranged.maxSpreadAngle = shape.WidestSpreadDegrees;
            ranged.shootNewRandomTargetsPerProjectile = shape.EachShotPicksItsOwnTarget;
            ranged.projectileVariation = shape.ProjectileVariation;

            ranged.projectileFollowsTarget = shape.ShotsFollowTheirTarget;
            ranged.projectileTargetsSelf = shape.FiresAtItself;
            ranged.sameFactionHealingPercentage = shape.HealsItsOwnSideBy;
            ranged.meleeDamageRadiusAtEntity = shape.AlsoHurtsThingsTouchingIt;

            ranged.onlyAttackWhenInCombat = shape.OnlyShootsWhenInCombat;
            ranged.interruptOnDamageTaken = shape.TakingAHitInterruptsIt;
            ranged.endDuration = shape.RecoveryAfterShooting;

            ranged.attackDuration = shape.HowLongItKeepsShooting;
            ranged.skipVisibilityCheck = shape.ShootsAtWhatItCannotSee;
            ranged.onlyAttackTargetsWeWantToAttack = shape.OnlyShootsThingsItWantsToAttack;
            ranged.disabled = shape.StartsSwitchedOff;
            ranged.startSpreadAngleOffset = shape.SpreadStartsAtDegrees;

            ranged.modifyBaseSpeedByTargetDistance = shape.SpeedChangesWithDistance;
            ranged.minMaxBaseSpeedMultiplierByTargetDistance = new Unity.Mathematics.float2(
                shape.SpeedFromNearToFar.x,
                shape.SpeedFromNearToFar.y);
            ranged.minMaxDistanceForBaseSpeedMultiplier = new Unity.Mathematics.float2(
                shape.NearAndFarDistance.x,
                shape.NearAndFarDistance.y);

            ranged.animOverride = shape.AnimationName;
            ranged.animPerShot = shape.AnimationPerShot;

            if (report == null)
            {
                return;
            }

            if (shape.SpreadWidthWillBeIgnored)
            {
                report(
                    "sets a widest spread on a shot pattern that never spreads, so the number does " +
                    "nothing. Only the random spread reads it.");
            }

            if (shape.LeadRangeIsHalfSpecified)
            {
                report(
                    "gives only one end of its target-leading range. The two are read together, so " +
                    "it will not lead a moving target at all. Set both, or leave both at zero.");
            }
            if (shape.SpeedByDistanceWillBeIgnored)
            {
                report(
                    "sets a near and far distance for its shot speed without switching that on, so " +
                    "its projectiles all travel at one speed.");
            }
        }

        public static void ApplyAttackSounds(
            GameObject root,
            DimensionAttackSoundsTemplate sounds,
            System.Action<string> report = null)
        {
            if (sounds == null || !sounds.HasAnySound)
            {
                RemoveComponentIfPresent<CustomAttackSoundAuthoring>(root);
                return;
            }

            // A typo in a sound name plays as silence, with nothing anywhere saying why. The name
            // still hashes — a mod shipping its own sounds needs that — so the most that can be
            // done is to say which names the game does not ship.
            if (report != null)
            {
                System.Collections.Generic.List<string> unknown = sounds.NamesTheGameDoesNotShip();
                for (int i = 0; i < unknown.Count; i++)
                {
                    report(
                        "names the sound '" + unknown[i] + "', which the game does not ship. " +
                        "If it is not a sound this mod brings itself, it will play as silence.");
                }
            }

            CustomAttackSoundAuthoring custom = EnsureComponent<CustomAttackSoundAuthoring>(root);
            custom.attackSoundId = new SFXTableIDField { value = sounds.AttackSound };
            custom.impactSoundId = new SFXTableIDField { value = sounds.ImpactSound };
            custom.windupSound = new SFXTableIDField { value = sounds.WindUpSound };
            custom.windupCancelSound = new SFXTableIDField { value = sounds.WindUpCancelledSound };
            custom.strongAttackSound = new SFXTableIDField { value = sounds.StrongAttackSound };
        }

        /// <summary>
        /// What hitting and breaking it sounds and looks like.
        /// </summary>
        /// <remarks>
        /// Removed when nothing is set, so an object stripped of its feedback does not keep throwing
        /// the dust it used to. An unknown puff name is reported rather than dropped, because a
        /// missing burst is invisible until somebody swings at the thing.
        /// </remarks>
        public static void ApplyImpactFeedback(
            GameObject root,
            DimensionImpactFeedbackTemplate feedback,
            System.Action<string> reportUnknownPuff)
        {
            if (feedback == null || !feedback.HasAnyFeedback)
            {
                RemoveComponentIfPresent<TileEffectAuthoring>(root);
                return;
            }

            TileEffectAuthoring effect = EnsureComponent<TileEffectAuthoring>(root);
            effect.sfxTableDamageId = new SFXTableIDField { value = feedback.HitSoundId };
            effect.sfxTableDestroyId = new SFXTableIDField { value = feedback.BreakSoundId };
            effect.destroyPuffs = new System.Collections.Generic.List<PuffParams>();

            DimensionPuffBurst[] bursts = feedback.BreakParticles;
            for (int i = 0; i < bursts.Length; i++)
            {
                PuffID puff;
                if (!System.Enum.TryParse(bursts[i].PuffId, false, out puff))
                {
                    if (reportUnknownPuff != null)
                    {
                        reportUnknownPuff(bursts[i].PuffId);
                    }

                    continue;
                }

                effect.destroyPuffs.Add(new PuffParams
                {
                    puff = puff,
                    particleCount = bursts[i].ParticleCount,
                    relativePosition = bursts[i].Offset
                });
            }
        }
    }
}
