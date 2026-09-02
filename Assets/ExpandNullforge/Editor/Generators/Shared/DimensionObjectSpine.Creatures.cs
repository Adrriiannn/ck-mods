using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The spine a creature carries: how it comes into the world and what it is made of.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        /// <summary>
        /// How a creature arrives, idles, scales with the party, and leaves.
        /// </summary>
        /// <remarks>
        /// Seven components in one call because they are one arc. Every one is added or removed
        /// rather than only added: a boss that stops scaling with the party must actually stop.
        /// </remarks>
        public static void ApplyCreatureLifecycle(
            GameObject root,
            DimensionCreatureLifecycleTemplate lifecycle,
            System.Action<string> report)
        {
            if (lifecycle == null)
            {
                lifecycle = new DimensionCreatureLifecycleTemplate();
            }

            ApplyEntrance(root, lifecycle, report);
            ApplyIdleEmotes(root, lifecycle);

            if (lifecycle.ScalesWithTheParty)
            {
                EnsureComponent<ScaleHealthByPlayerCountAuthoring>(root).scalingFactor =
                    lifecycle.HealthScalesWithPlayers;
            }
            else
            {
                RemoveComponentIfPresent<ScaleHealthByPlayerCountAuthoring>(root);
            }

            if (lifecycle.FollowsALure)
            {
                FollowPheromoneStateAuthoring follow =
                    EnsureComponent<FollowPheromoneStateAuthoring>(root);
                follow.pheromonesToFollow = new System.Collections.Generic.List<PheromoneType>
                {
                    PheromoneType.Player
                };
            }
            else
            {
                RemoveComponentIfPresent<FollowPheromoneStateAuthoring>(root);
            }

            Toggle<CanBeControlledByOtherEntityAuthoring>(
                root,
                lifecycle.SomethingElseCanControlIt);

            if (lifecycle.EverDespawns)
            {
                DestroyWhenNoNearbyPlayerAuthoring despawn =
                    EnsureComponent<DestroyWhenNoNearbyPlayerAuthoring>(root);
                despawn.distance = lifecycle.DespawnsWhenNobodyIsWithin;
                despawn.destroyDelay = lifecycle.DespawnDelaySeconds;
            }
            else
            {
                RemoveComponentIfPresent<DestroyWhenNoNearbyPlayerAuthoring>(root);
            }

            if (lifecycle.DeathClearsThingsNearby)
            {
                if (lifecycle.DeathClearsNothing && report != null)
                {
                    report("clears things when it dies over a radius of nothing.");
                }

                DestroyNearbyOnDeathAuthoring clears =
                    EnsureComponent<DestroyNearbyOnDeathAuthoring>(root);
                clears.radius = lifecycle.DeathClearRadius;
                clears.killAnyTemporaryEnemy = lifecycle.ItsSummonsGoWithIt;
                clears.destroyEntitiesWithDontDestroyOnZeroHealthCD =
                    lifecycle.DeathTakesEvenTheUnkillable;
                if (clears.objectsToDestroy == null)
                {
                    clears.objectsToDestroy = new System.Collections.Generic.List<ObjectID>();
                }
            }
            else
            {
                RemoveComponentIfPresent<DestroyNearbyOnDeathAuthoring>(root);
            }
        }

        /// <param name="eggIsAnsweredElsewhere">
        /// True when the thing being written already has a better place to say it is an egg. A
        /// creature does: <c>DimensionCreatureGenerator.ApplyHatching</c> owns
        /// <c>HatchWhenPlayerNearbyStateAuthoring</c> on a creature, and it is the only one of the
        /// two that can name one of the MOD'S own creatures to hatch into — it writes a name beside
        /// the id for the hydration system to fill in later. This pass runs after that one, so
        /// without this flag its else-branch would remove the hatching the creature's own answer had
        /// just written, and every generated egg would quietly stop hatching.
        /// </param>
        public static void ApplyHidingAndHatching(
            GameObject root,
            DimensionHidingAndHatchingTemplate world,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            bool eggIsAnsweredElsewhere = false)
        {
            if (root == null || world == null)
            {
                return;
            }

            Toggle<LarvaHiveBossHatchEggStateAuthoring>(root, world.HatchesEggsLikeTheLarvaHive);

            if (world.HidesInBushes)
            {
                BushStateAuthoring bush = EnsureComponent<BushStateAuthoring>(root);
                bush.goToBushDuration = world.SecondsToReachABush;
                bush.randomlyLeaveStateMinDuration = world.MinSecondsHidden;
                bush.randomlyLeaveStateMaxDuration = world.MaxSecondsHidden;
                bush.peakDuration = world.PeekSeconds;
                bush.leaveDuration = world.SecondsToLeave;
                bush.distanceToTargetToLeaveState = world.ComesOutWithin;
                bush.burrowWhenOutOfCombatDelay = world.BurrowsBackAfter;
            }
            else
            {
                RemoveComponentIfPresent<BushStateAuthoring>(root);
            }

            // NOT TOUCHED AT ALL when something else owns the egg. Not "written anyway and hope
            // they agree": the two answers would fight over the same component, and only the other
            // one can carry a name for one of the mod's own creatures.
            if (eggIsAnsweredElsewhere)
            {
                SayWhenTicked(
                    world.IsAnEgg,
                    report,
                    "is set to be an egg here as well as under its own hatching answers. A creature " +
                    "hatches from Hatching, which is the one that can name a creature of your own " +
                    "to hatch into, so this tick was left alone and changed nothing. Untick it and " +
                    "answer it under Hatching.");
            }
            else if (world.IsAnEgg && !world.HatchesIntoNothing)
            {
                ObjectID hatched = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.HatchesIntoId);
                if (hatched == ObjectID.None)
                {
                    RemoveComponentIfPresent<HatchWhenPlayerNearbyStateAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "hatches into '" + world.HatchesIntoId + "', which the game does not " +
                            "have, so nothing comes out of it.");
                    }
                }
                else
                {
                    HatchWhenPlayerNearbyStateAuthoring egg =
                        EnsureComponent<HatchWhenPlayerNearbyStateAuthoring>(root);
                    egg.timeToHatch = world.SecondsToHatch;
                    egg.objectToSpawn = hatched;
                    egg.minSpawnAmount = world.FewestHatched;
                    egg.maxSpawnAmount = world.MostHatched;
                }
            }
            else
            {
                RemoveComponentIfPresent<HatchWhenPlayerNearbyStateAuthoring>(root);
            }

            if (world.CavelingTerritorySize > 0)
            {
                CavelingTerritorySpawnerAuthoring caveling =
                    EnsureComponent<CavelingTerritorySpawnerAuthoring>(root);
                caveling.size = world.CavelingTerritorySize;
                caveling.cavelingSpawnChance = world.CavelingChance;
                caveling.cavelingShamanSpawnChance = world.CavelingShamanChance;
                caveling.cavelingBruteSpawnChance = world.CavelingBruteChance;
            }
            else
            {
                RemoveComponentIfPresent<CavelingTerritorySpawnerAuthoring>(root);
            }

            if (world.FiresADelayedShot)
            {
                IndirectProjectileAuthoring shot =
                    EnsureComponent<IndirectProjectileAuthoring>(root);
                shot.delayTime = world.ShotDelay;
                shot.speed = world.ShotSpeed;
                shot.seeking = world.ShotSeeks;
            }
            else
            {
                RemoveComponentIfPresent<IndirectProjectileAuthoring>(root);
            }

            SayWhenTicked(
                world.FiresADelayedShot,
                report,
                "is set to fire a delayed shot. Core Keeper only steers a shot like that on " +
                "something that IS a shot — it has to be a projectile, sent to players, with a " +
                "direction, a speed and a notice range — so on a placed object the delay and the " +
                "seeking do nothing. Build it as a projectile and give the weapon that fires it.");

            if (world.TriggersOnApproach)
            {
                ProximityTriggerAuthoring trigger =
                    EnsureComponent<ProximityTriggerAuthoring>(root);
                trigger.radius = world.TriggersWithin;
                trigger.delayTime = world.TriggerDelay;
            }
            else
            {
                RemoveComponentIfPresent<ProximityTriggerAuthoring>(root);
            }

            AnimationSpeedAuthoring animation = root.GetComponent<AnimationSpeedAuthoring>();
            if (!Mathf.Approximately(world.AnimationSpeed, 1f) ||
                !Mathf.Approximately(world.AnimationLeanX, 0f) ||
                !Mathf.Approximately(world.AnimationLeanY, 0f))
            {
                animation = EnsureComponent<AnimationSpeedAuthoring>(root);
                animation.speed = world.AnimationSpeed;
                animation.movementX = world.AnimationLeanX;
                animation.movementY = world.AnimationLeanY;

                SayWhenTicked(
                    true,
                    report,
                    "has its own animation speed and lean. Core Keeper works those out from how " +
                    "the PLAYER is moving and what state they are in, and reads them off nothing " +
                    "else, so the numbers here will not change how this object animates.");
            }
            else if (animation != null)
            {
                RemoveComponentIfPresent<AnimationSpeedAuthoring>(root);
            }

            if (world.HasSellSlots)
            {
                SellSlotsAuthoring sell = EnsureComponent<SellSlotsAuthoring>(root);
                sell.sizeX = world.SellColumns;
                sell.sizeY = world.SellRows;
            }
            else
            {
                RemoveComponentIfPresent<SellSlotsAuthoring>(root);
            }

            Toggle<UpgradeSlotAuthoring>(root, world.HasAnUpgradeSlot);
            Toggle<VanitySlotsAuthoring>(root, world.HasVanitySlots);

            // The three of them are the same finding. Core Keeper builds a sell window, a vanity
            // window and an upgrade window for the player and for nothing else — the code that
            // opens each one is only ever handed the player — so the slots sit on the object and
            // no window is ever built from them.
            SayWhenTicked(
                world.HasSellSlots || world.HasAnUpgradeSlot || world.HasVanitySlots,
                report,
                "has sell, upgrade or vanity slots. Core Keeper only ever builds those windows for " +
                "the player, so the slots will exist on the object and nothing will open them. " +
                "For a shop, use the trader answers; for storage, use a container.");

            if (report == null)
            {
                return;
            }

            // Silent when the egg is somebody else's answer: the sentence above already said the
            // tick did nothing, and following it with two more complaints about how it was filled
            // in reads as three problems instead of one.
            if (world.HatchesIntoNothing && !eggIsAnsweredElsewhere)
            {
                report("is an egg that hatches into nothing.");
            }

            if (world.HatchesNone && !eggIsAnsweredElsewhere)
            {
                report("is an egg that hatches none of what it hatches into.");
            }

            if (world.CavelingTerritoryIsEmpty)
            {
                report(
                    "claims a caveling territory with no chance of a caveling, shaman or brute " +
                    "appearing in it, so the territory stays empty.");
            }

            if (world.DelayedShotNeverMoves)
            {
                report("fires a delayed shot with no speed, so it waits and then sits there.");
            }
        }

        public static void ApplySegmentedCreature(
            GameObject root,
            DimensionSegmentedCreatureTemplate segmented,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            if (root == null || segmented == null || !segmented.IsSegmented || segmented.HasNoBody)
            {
                RemoveComponentIfPresent<SnakeMovementStateAuthoring>(root);
                if (segmented != null && segmented.HasNoBody && report != null)
                {
                    report(
                        "is a segmented creature with no segments, so it is just a head. Give it a " +
                        "starting length.");
                }

                return;
            }

            SnakeMovementStateAuthoring snake = EnsureComponent<SnakeMovementStateAuthoring>(root);

            snake.initialLength = segmented.StartingLength;
            snake.spread = segmented.Spacing;
            snake.additionalHorizontalSpread = segmented.ExtraSidewaysSpacing;
            snake.treatSegmentsAsIndividualParts = segmented.SegmentsAreHitSeparately;

            snake.turnDuration = segmented.TurnSeconds;
            snake.wavinessAmplitude = segmented.WeaveWidth;
            snake.wavinessTurnTime = segmented.WeaveSeconds;
            snake.chaoticMovement = segmented.MovesChaotically;
            snake.slowDownForWalls = segmented.SlowsNearWalls;
            snake.descendIntoPits = segmented.GoesIntoPits;
            snake.playMoveAnimation = segmented.PlaysItsMoveAnimation;
            snake.usePhysVelocity = segmented.MovedByPhysics;

            snake.useCaterpillarMovement = segmented.BunchesLikeACaterpillar;
            snake.stretchOutStrength = segmented.StretchOutStrength;
            snake.stretchBackStrength = segmented.PullBackStrength;
            snake.stretchFrequency = segmented.BunchSpeed;
            snake.stretchSpread = segmented.BunchSpread;

            snake.targetingType = (SnakeTargetingType)(int)segmented.PicksTarget;
            snake.distanceToAttackPlayer = segmented.NoticesPlayersWithin;
            snake.distanceToTargetToChangeTarget = segmented.SwitchesTargetWithin;
            snake.playerTargetCooldownMin = segmented.MinTargetCooldown;
            snake.playerTargetCooldownMax = segmented.MaxTargetCooldown;
            snake.distanceAllowedToMoveAwayFromCombatStartPosition =
                segmented.StraysFromTheFightBy;

            snake.disableDamage = segmented.DealsNoDamage;
            snake.damage = segmented.FlatDamage;
            snake.damageMultiplier = segmented.HitsThisHardForItsTier;
            snake.attackRadius = segmented.HitRadius;
            snake.attackOffset = segmented.HitOffset;
            snake.pushbackForce = segmented.ShoveForce;
            snake.tooCloseDistanceForAttack = segmented.TooCloseToAttack;
            snake.dontDropLootFromObjectsBeingDestroyed = segmented.WhatItSmashesDropsNothing;

            snake.tilePlacementType =
                (SnakeMovementTilePlacementType)(int)segmented.LeavesBehind;
            snake.tilePlacementRadiusMultiplier = segmented.TrailWidthMultiplier;

            snake.tailObjectId = string.IsNullOrEmpty(segmented.TailObjectId)
                ? ObjectID.None
                : (resolveObject == null ? ObjectID.None : resolveObject(segmented.TailObjectId));

            snake.cantHitSpecificObject = string.IsNullOrEmpty(segmented.NeverHits)
                ? ObjectID.None
                : (resolveObject == null ? ObjectID.None : resolveObject(segmented.NeverHits));

            if (report == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(segmented.TailObjectId) &&
                snake.tailObjectId == ObjectID.None &&
                !(isDeferred != null && isDeferred(segmented.TailObjectId)))
            {
                report(
                    "ends in '" + segmented.TailObjectId + "', which is neither one of this mod's " +
                    "objects nor one the game has, so its body simply stops instead.");
            }

            if (!string.IsNullOrEmpty(segmented.NeverHits) &&
                snake.cantHitSpecificObject == ObjectID.None &&
                !(isDeferred != null && isDeferred(segmented.NeverHits)))
            {
                report(
                    "is set never to hit '" + segmented.NeverHits + "', which is neither one of " +
                    "this mod's objects nor one the game has, so it will hit everything.");
            }

            if (segmented.BunchingWillBeIgnored)
            {
                report(
                    "has its bunching shaped without caterpillar movement switched on, so its body " +
                    "flows instead and none of those four settings are read.");
            }

            if (segmented.TrailWidthWillBeIgnored)
            {
                report("sets how wide its trail is while leaving no trail.");
            }

            if (segmented.WeaveIsHalfSpecified)
            {
                report(
                    "gives its weave a width but no time, or a time but no width. Both are needed, " +
                    "so it will travel straight.");
            }
        }

        public static void ApplyNest(
            GameObject root,
            DimensionNestTemplate nest,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            if (root == null || nest == null || !nest.IsANest || nest.ProducesNothing)
            {
                RemoveComponentIfPresent<SpawnAroundObjectAuthoring>(root);
                if (nest != null && nest.ProducesNothing && report != null)
                {
                    report("is a nest that produces nothing, so it just sits there.");
                }

                return;
            }

            SpawnAroundObjectAuthoring spawner = EnsureComponent<SpawnAroundObjectAuthoring>(root);
            spawner.spawnEntries =
                new System.Collections.Generic.List<SpawnAroundObjectAuthoring.SpawnEntry>();

            DimensionNestBrood[] broods = nest.Broods;
            for (int i = 0; i < broods.Length; i++)
            {
                ObjectID made = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(broods[i].ObjectId);
                if (made == ObjectID.None)
                {
                    if (report != null)
                    {
                        report(
                            "produces '" + broods[i].ObjectId + "', which the game does not have, " +
                            "so that one is left out of the nest.");
                    }

                    continue;
                }

                SpawnAroundObjectAuthoring.SpawnEntry entry =
                    new SpawnAroundObjectAuthoring.SpawnEntry
                    {
                        objectToSpawn = new ObjectData
                        {
                            objectID = made,
                            variation = broods[i].Variation,
                            amount = 1
                        },
                        limitNumberSpawned = broods[i].AtMostAtOnce,
                        minSpawnCooldown = broods[i].MinWait,
                        maxSpawnCooldown = broods[i].MaxWait,
                        maxSpawnDistance = broods[i].AppearsWithin,
                        minReachedLimitCooldown = broods[i].MinWaitWhenFull,
                        maxReachedLimitCooldown = broods[i].MaxWaitWhenFull,
                        onlySpawnIfInCombat = broods[i].OnlyWhileFighting,
                        spawnCloseToPlayers = broods[i].AppearsNearPlayers,
                        playerNeedsToBeInsideBiome = broods[i].PlayerMustBeInThatBiome,
                        spawnCrittersInsteadOfObject = broods[i].ProducesCritters,
                        critterDespawnDistance = broods[i].CritterDespawnDistance,
                        objectIsPersistent = broods[i].WhatItMakesPersists,
                        spawnsInBiome = new System.Collections.Generic.List<Biome>()
                    };

                string[] biomes = broods[i].OnlyInBiomes;
                for (int b = 0; b < biomes.Length; b++)
                {
                    Biome biome;
                    if (System.Enum.TryParse(biomes[b], false, out biome))
                    {
                        entry.spawnsInBiome.Add(biome);
                    }
                    else if (report != null)
                    {
                        report(
                            "produces in biome '" + biomes[b] + "', which the game does not have, " +
                            "so it will not produce there.");
                    }
                }

                Season season;
                if (!string.IsNullOrEmpty(broods[i].OnlyInSeason) &&
                    System.Enum.TryParse(broods[i].OnlyInSeason, false, out season))
                {
                    entry.onlySpawnsInSeason = season;
                }

                ConditionID needed;
                if (!string.IsNullOrEmpty(broods[i].RequiresCondition) &&
                    System.Enum.TryParse(broods[i].RequiresCondition, false, out needed))
                {
                    entry.requiredCondition = needed;
                }

                if (!string.IsNullOrEmpty(broods[i].KeepsAwayFrom))
                {
                    entry.avoidSpawnCloseToObject = resolveObject == null
                        ? ObjectID.None
                        : resolveObject(broods[i].KeepsAwayFrom);
                }

                spawner.spawnEntries.Add(entry);

                if (broods[i].BiomeRuleIsIncomplete && report != null)
                {
                    report(
                        "requires the player to be standing in its biome without naming any biome, " +
                        "so that rule never lets anything through.");
                }
            }

            if (spawner.spawnEntries.Count == 0)
            {
                RemoveComponentIfPresent<SpawnAroundObjectAuthoring>(root);
            }
        }

        /// <summary>
        /// Makes a creature something somebody else summoned, and gives it a summoner's numbers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE OTHER "IS A MINION" DOES NOTHING, and that is why this exists. There have been two
        /// controls with that name for a while: the one under simple traits writes
        /// <c>MinionDataAuthoring</c>, which Core Keeper never turns into anything and which the
        /// framework already says so about; and the real one, which writes <c>MinionAuthoring</c>
        /// and lives in the world-object roles the creature generator never calls. Two controls
        /// with one label and different power is worse than one control, so the creature now has
        /// the real one where a creature can reach it.
        /// </para>
        /// <para>
        /// IT CARRIES MORE THAN THE MULTIPLIERS. <c>MinionConverter</c> ensures
        /// <c>OwnerReferenceCD</c>, and that component is what <c>TouchAttackStateSystem</c> and
        /// <c>MinionOrbitStateSystem</c> name in their queries — so a creature that hurts what it
        /// touches, or orbits whoever summoned it, only works at all once it is a minion or a pet.
        /// It also reads <c>IsFlyingAuthoring</c> off the object, which is written by the creature's
        /// habits, so this must run after those.
        /// </para>
        /// </remarks>
        public static void ApplyCreatureMinion(
            GameObject root,
            DimensionCreatureMinionTemplate minion,
            System.Action<string> report)
        {
            if (root == null || minion == null)
            {
                return;
            }

            WriteMinion(
                root,
                minion.IsAMinion,
                minion.HitsThisHardForItsTier,
                minion.MinesToo,
                minion.MinesThisHardForItsTier,
                report,
                true);
        }

        /// <summary>
        /// Puts the minion component on, or takes it off, and says what is missing beside it.
        /// </summary>
        /// <param name="onACreature">
        /// Changes only what is said. On a world object the advice is "build it as a creature,
        /// where the attacks are"; on a creature that advice would be nonsense.
        /// </param>
        private static void WriteMinion(
            GameObject root,
            bool isAMinion,
            float damageMultiplier,
            bool minesToo,
            float miningDamageMultiplier,
            System.Action<string> report,
            bool onACreature = false)
        {
            if (!isAMinion)
            {
                RemoveComponentIfPresent<MinionAuthoring>(root);
                return;
            }

            MinionAuthoring minion = EnsureComponent<MinionAuthoring>(root);
            minion.damageMultiplier = damageMultiplier;

            // ONLY WHEN THERE IS A MELEE ATTACK TO MINE WITH. Core Keeper's own step prints a
            // console error at conversion time when a minion is told to mine and has no melee
            // attack, and a red line in the console with no name attached to it is worse than
            // useless to somebody building a mod.
            minion.hasMiningAttack = minesToo && HasNamed(root, "MeleeAttackStateAuthoring");
            minion.miningDamageMultiplier = miningDamageMultiplier;

            SayWhenTicked(
                minesToo && !minion.hasMiningAttack,
                report,
                onACreature
                    ? "is a minion that also mines, but it has no melee attack to mine with, and " +
                      "Core Keeper refuses that outright. Give it a melee attack."
                    : "is a minion that also mines, but it has no melee attack to mine with, and " +
                      "Core Keeper refuses that outright. Build it as a creature and give it a " +
                      "melee attack.");

            SayWhenTicked(
                !HasNamed(root, "MeleeAttackStateAuthoring") &&
                !HasNamed(root, "RangeAttackStateAuthoring"),
                report,
                onACreature
                    ? "is a minion with no attack. A minion in Core Keeper is fought through: the " +
                      "game hands it a target and then looks for its melee or ranged attack to " +
                      "use. Give it one under its attacks."
                    : "is a minion with no attack. A minion in Core Keeper is fought through: the " +
                      "game hands it a target and then looks for its melee or ranged attack to " +
                      "use. Build it as a creature, where the attacks are.");
        }

        /// <summary>
        /// Lets a creature drop at zero health and get back up, instead of dying.
        /// </summary>
        /// <remarks>
        /// The same component and the same reasoning as the world object's answer under its rules
        /// (see <see cref="ApplyObjectRules"/>), reachable from a creature, which is where the two
        /// clips it fires are actually worth drawing. <c>AnimateDontDestroyOnZeroHealthSystem</c>
        /// asks for health, the animate component, the animation buffer and its pointer, and a
        /// generated creature has all four before this runs.
        /// </remarks>
        public static void ApplyCreatureLastStand(
            GameObject root,
            DimensionCreatureLastStandTemplate lastStand,
            System.Action<string> report)
        {
            if (root == null || lastStand == null)
            {
                return;
            }

            // ON THE WAY ON ONLY, exactly as the world-object answer does it: the component makes
            // the creature survive zero health, so unticking clears the flag rather than quietly
            // making something mortal that a borrowed kit authored not to be.
            DontDestroyOnZeroHealthAuthoring survives = lastStand.PlaysDeadInsteadOfDying
                ? EnsureComponent<DontDestroyOnZeroHealthAuthoring>(root)
                : root.GetComponent<DontDestroyOnZeroHealthAuthoring>();
            if (survives != null)
            {
                survives.animate = lastStand.PlaysDeadInsteadOfDying;
            }

            SayWhenTicked(
                lastStand.PlaysDeadInsteadOfDying,
                report,
                "plays dead instead of dying. At zero health the game stops short of destroying " +
                "it: it drops, and it stands back up if anything ever heals it. It will not drop " +
                "loot and it will not disappear, because it never actually dies, so something " +
                "else has to take it away. Draw 'Playing dead' and 'Getting back up' for it.");
        }

        /// <summary>
        /// Makes a creature a pet: it follows its owner, fights alongside them, and buffs them.
        /// </summary>
        /// <remarks>
        /// The walk state comes with it. A pet paths to its owner rather than to a target, so the
        /// ordinary chase is the wrong movement — a pet without <c>PetWalkStateAuthoring</c> stands
        /// where it was summoned and never follows anybody.
        /// </remarks>
        public static void ApplyPet(
            GameObject root,
            DimensionPetTemplate pet,
            System.Action<string> report)
        {
            if (root == null || pet == null || !pet.IsAPet)
            {
                RemoveComponentIfPresent<PetAuthoring>(root);
                RemoveComponentIfPresent<PetWalkStateAuthoring>(root);
                if (pet != null && pet.TalentsWillBeIgnored && report != null)
                {
                    report(
                        "lists pet talents without being a pet, so none of them reach a player.");
                }

                return;
            }

            PetAuthoring authored = EnsureComponent<PetAuthoring>(root);
            authored.petType = (PetType)(int)pet.FightsBy;
            authored.isFlying = pet.Flies;
            authored.happyAnimDuration = pet.HappyAnimationSeconds;

            authored.petTalents = new System.Collections.Generic.List<PetTalent>();
            string[] talents = pet.Talents;
            for (int i = 0; i < talents.Length; i++)
            {
                PetTalent talent;
                if (System.Enum.TryParse(talents[i], false, out talent))
                {
                    authored.petTalents.Add(talent);
                }
                else if (report != null)
                {
                    report(
                        "has pet talent '" + talents[i] + "', which the game does not have, so that " +
                        "one gives its owner nothing.");
                }
            }

            PetWalkStateAuthoring walk = EnsureComponent<PetWalkStateAuthoring>(root);
            walk.belongsToShape = root.GetComponent<Unity.Physics.Authoring.PhysicsShapeAuthoring>();
        }
    }
}
