using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The spine a boss carries, and the spawner and orb that go with it.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        /// <param name="onACreature">
        /// True when the thing being written is a creature rather than something placed. It changes
        /// nothing that is written; it changes what is SAID. Two of the notes below tell a placed
        /// object that wandering and roaming are creature work and to build it as a creature — true
        /// advice on a chest, and a flat lie once the creature generator is the caller.
        /// </param>
        public static void ApplySpawnerAndOrb(
            GameObject root,
            DimensionSpawnerAndOrbTemplate world,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            bool onACreature = false)
        {
            if (root == null || world == null)
            {
                return;
            }

            // NOTHING IN CORE KEEPER READS THIS MARK, and the object generator's own companion row
            // has said so for a while without the writer saying anything. `SpawnerCD` is declared
            // in `ck-db\Pug.ECS.Components\SpawnerCD.cs` and no system anywhere reads it, so every
            // number under the plain spawner is a number that goes nowhere. Said here as well as in
            // the sweep because an author filling in five spawn distances deserves to hear it from
            // the thing they are filling in.
            SayWhenTicked(
                world.IsAPlainSpawner,
                report,
                "is set up as a plain spawner. Nothing in Core Keeper reads that mark — the " +
                "component it becomes has no reader anywhere in the game — so it will spawn " +
                "nothing however the distances are set. Use a larva or slime territory, a nest, or " +
                "a spawner platform instead.");

            if (world.IsAPlainSpawner)
            {
                SpawnerAuthoring spawner = EnsureComponent<SpawnerAuthoring>(root);
                spawner.minSpawnDistance = world.SpawnsNoCloserThan;
                spawner.maxSpawnDistance = world.SpawnsNoFurtherThan;
                spawner.maxNumberSpawned = world.KeepsTrackOf;
                spawner.forgetWhenThisFarAway = world.ForgetsBeyond;
                spawner.disableSpawnWhenStationary = world.StopsWhenStill;
            }
            else
            {
                RemoveComponentIfPresent<SpawnerAuthoring>(root);
            }

            if (world.LarvaTerritorySize > 0)
            {
                EnsureComponent<LarvaTerritorySpawnerAuthoring>(root).size =
                    world.LarvaTerritorySize;
            }
            else
            {
                RemoveComponentIfPresent<LarvaTerritorySpawnerAuthoring>(root);
            }

            if (world.SlimeTerritorySize > 0)
            {
                SlimeTerritorySpawnerAuthoring slime =
                    EnsureComponent<SlimeTerritorySpawnerAuthoring>(root);
                slime.size = world.SlimeTerritorySize;
                slime.slimeBlobSpawnChance = world.SlimeBlobChance;
            }
            else
            {
                RemoveComponentIfPresent<SlimeTerritorySpawnerAuthoring>(root);
            }

            if (world.IsADriftingOrb && !world.OrbHasNoPattern)
            {
                ElectricOrbAuthoring orb = EnsureComponent<ElectricOrbAuthoring>(root);
                orb.startDuration = world.OrbAppearSeconds;
                orb.loopDuration = world.OrbDriftSeconds;
                orb.endDuration = world.OrbFadeSeconds;
                orb.hiddenEndDuration = world.OrbHiddenSeconds;
                orb.bounceOnWalls = world.OrbBouncesOffWalls;
                orb.movementPatterns =
                    new System.Collections.Generic.List<ElectricOrbAuthoring.MovementPattern>();

                DimensionOrbDrift[] drifts = world.OrbPatterns;
                for (int i = 0; i < drifts.Length; i++)
                {
                    ElectricOrbMovementPattern named;
                    if (!System.Enum.TryParse(drifts[i].Pattern, false, out named))
                    {
                        if (report != null)
                        {
                            report(
                                "drifts using pattern '" + drifts[i].Pattern + "', which the game " +
                                "does not have, so that stretch is left out.");
                        }

                        continue;
                    }

                    orb.movementPatterns.Add(new ElectricOrbAuthoring.MovementPattern
                    {
                        pattern = named,
                        minMaxDurationSeconds = drifts[i].SecondsRange,
                        minMaxSpeed = drifts[i].SpeedRange,
                        sinusoidalPattern = drifts[i].Weaves,
                        sinusoidalMaxTurnAngleDegrees = drifts[i].WeaveAngle,
                        sinusoidalRepeatTimePerSecond = drifts[i].WeaveRate
                    });
                }

                if (orb.movementPatterns.Count == 0)
                {
                    RemoveComponentIfPresent<ElectricOrbAuthoring>(root);
                }
            }
            else
            {
                RemoveComponentIfPresent<ElectricOrbAuthoring>(root);
            }

            SayWhenTicked(
                !onACreature && world.WandersNearSomething && !world.WandersNearNothing,
                report,
                "wanders near something, but wandering in Core Keeper is something a creature " +
                "does: the game needs a walking speed and a body that can be pushed, and a placed " +
                "object has neither. Build it as a creature and it will wander.");

            if (world.WandersNearSomething && !world.WandersNearNothing)
            {
                ObjectID near = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.StaysNearObjectId);
                if (near == ObjectID.None)
                {
                    RemoveComponentIfPresent<RandomFollowStateAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "wanders near '" + world.StaysNearObjectId + "', which the game does " +
                            "not have, so it wanders freely instead.");
                    }
                }
                else
                {
                    RandomFollowStateAuthoring follow =
                        EnsureComponent<RandomFollowStateAuthoring>(root);
                    follow.objectToFollow = near;
                    follow.minDistanceFromObjectToFollow = world.StaysNoCloserThan;
                    follow.maxDistanceFromObjectToFollow = world.StraysNoFurtherThan;
                    follow.maxWalkDuration = world.WalksForAtMost;
                    follow.minIdleDuration = world.RestsAtLeast;
                    follow.maxIdleDuration = world.RestsAtMost;
                }
            }
            else
            {
                RemoveComponentIfPresent<RandomFollowStateAuthoring>(root);
            }

            if (world.IsABossStatue && !world.StatueAcceptsNothing)
            {
                ObjectID crystal = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.AcceptsCrystalId);
                if (crystal == ObjectID.None)
                {
                    RemoveComponentIfPresent<BossStatueAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "is a statue accepting '" + world.AcceptsCrystalId + "', which the game " +
                            "does not have, so nothing will light it.");
                    }
                }
                else
                {
                    BossStatueAuthoring statue = EnsureComponent<BossStatueAuthoring>(root);
                    statue.acceptsCrystalID = crystal;
                    statue.electricityLoadUpTimer = world.StatueChargeSeconds;
                }
            }
            else
            {
                RemoveComponentIfPresent<BossStatueAuthoring>(root);
            }

            SayWhenTicked(
                !onACreature && world.ChewsGroundAsItRoams,
                report,
                "chews the ground as it roams, but roaming in Core Keeper is something a creature " +
                "does: the game needs a walking speed, a body that can be pushed and a patrol " +
                "route to work over, and a placed object has none of the three. Build it as a " +
                "creature and it will roam.");

            // ON A CREATURE THE ADVICE IS THE OTHER HALF OF THE SAME SENTENCE. Roaming really is
            // creature work, and the third of the three things it needs is the route:
            // `RoamingStateSystem.cs:152` names `RoamingPathBuffer` in its query, and the only
            // thing in the game that produces one is `RoamingPathAuthoring`, which this framework
            // writes from the patrol route and nowhere else. The companion sweep fills a route in
            // if the author left it blank; this says so, so a route that appeared from nowhere is
            // not a surprise.
            SayWhenTicked(
                onACreature && world.ChewsGroundAsItRoams,
                report,
                "chews the ground as it roams. Roaming is walked along a route, so it needs one " +
                "under 'Where it walks' — without a route the game never looks at it at all. If " +
                "you left the route blank, a wandering circle was filled in for you, the same " +
                "shape the game's own worm roams in.");

            if (world.ChewsGroundAsItRoams)
            {
                RoamingStateAuthoring roam = EnsureComponent<RoamingStateAuthoring>(root);
                roam.tileDamageRadius = world.ChewRadius;
                roam.distanceInfrontToDamageTiles = world.ChewReach;
                roam.cantHitSpecificObjects = new System.Collections.Generic.List<ObjectID>();

                string[] spared = world.NeverBreaks;
                for (int i = 0; i < spared.Length; i++)
                {
                    ObjectID safe = resolveObject == null
                        ? ObjectID.None
                        : resolveObject(spared[i]);
                    if (safe != ObjectID.None)
                    {
                        roam.cantHitSpecificObjects.Add(safe);
                    }
                    else if (report != null)
                    {
                        report(
                            "spares '" + spared[i] + "' while roaming, which the game does not " +
                            "have, so it will break everything.");
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<RoamingStateAuthoring>(root);
            }

            if (world.PullsThingsIn)
            {
                RandomWalkGravityWellAuthoring well =
                    EnsureComponent<RandomWalkGravityWellAuthoring>(root);
                well.radius = world.PullReaches;

                // ZERO PULLS NOTHING, AND THE NUMBER MEANS NOTHING ELSE. RandomWalkGravitySystem
                // runs its overlap against a filter it hardcodes and never reads this field as a
                // layer mask at all — the one place it appears is `(attractMask & attractMask) != 0`,
                // so it is a yes-or-no. UpdateFactionSystem writes 1 or 2 into it for a player, and
                // across the 57 vanilla prefabs that carry one the only values are 1 (16 of them)
                // and 2 (41). An earlier pass filled it with Category03|Category15 off a prefab
                // that does not carry those, and told the author it was "pulling on creatures and
                // critters", which was untrue twice over.
                well.attractMask = world.PullsOnLayers != 0
                    ? (uint)world.PullsOnLayers
                    : AGravityWellThatActuallyPulls;

                SayWhenTicked(
                    world.PullsOnLayers == 0,
                    report,
                    "pulls things in and was left at 0, which pulls nothing at all. It was " +
                    "generated at 1, the number the game itself writes. The number has no other " +
                    "meaning — the game only checks it is not 0 — so anything above 0 pulls the " +
                    "same things.");
            }
            else
            {
                RemoveComponentIfPresent<RandomWalkGravityWellAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (world.OrbHasNoPattern)
            {
                report("drifts as an orb with no movement patterns, so it never moves.");
            }

            if (world.WandersNearNothing)
            {
                report("wanders near something without naming what.");
            }

            if (world.StatueAcceptsNothing)
            {
                report("is a boss statue that accepts no crystal, so nothing can light it.");
            }
        }

        public static void ApplyMoreBossKits(
            GameObject root,
            DimensionMoreBossKitsTemplate kit,
            System.Action<string> report)
        {
            if (root == null || kit == null)
            {
                return;
            }

            Toggle<OctopusBossTeleportLocationAuthoring>(root, kit.IsAnOctopusSurfacingSpot);
            Toggle<BossLarvaSpawnStateAuthoring>(root, kit.ArrivesLikeTheBossLarva);

            if (kit.FightsLikeTheBird)
            {
                BirdBossAuthoring bird = EnsureComponent<BirdBossAuthoring>(root);
                bird.landDuration = kit.BirdLandSeconds;
                bird.durationBeforeStartingToSpawnStones = kit.BirdSecondsBeforeStones;
                bird.durationBeforeLeaveStonesSpawnState = kit.BirdSecondsBeforeStopping;
                bird.beamSpawn = new BirdBossAuthoring.SpawnConfiguration
                {
                    durationUntilSpawn = kit.BirdBeamWindUp,
                    durationAfterSpawn = kit.BirdBeamRecovery,
                    minCooldown = kit.BirdBeamMinCooldown,
                    maxCooldown = kit.BirdBeamMaxCooldown
                };
                bird.stoneSpawn = new BirdBossAuthoring.SpawnConfiguration
                {
                    durationUntilSpawn = kit.BirdStoneWindUp,
                    durationAfterSpawn = kit.BirdStoneRecovery,
                    minCooldown = kit.BirdStoneMinCooldown,
                    maxCooldown = kit.BirdStoneMaxCooldown
                };
            }
            else
            {
                RemoveComponentIfPresent<BirdBossAuthoring>(root);
            }

            if (kit.FightsLikeTheRobot)
            {
                RobotBossAuthoring robot = EnsureComponent<RobotBossAuthoring>(root);
                robot.legBrokenTime = kit.BrokenLegSeconds;
                robot.legXOffset = kit.LegSideOffset;
                robot.legZOffset = kit.LegDepthOffset;
                robot.maxStepHeight = kit.StepHeight;
                robot.stepHeightProgressMultiplier = kit.StepHeightCurve;
                robot.distanceToTriggerLegMovement = kit.DistanceBeforeALegMoves;
                robot.legMovementSpeed = kit.LegSpeed;
                robot.stepForwardDistance = kit.StepLength;
                robot.startDistance = kit.LegStartDistance;
                robot.legStepCooldownDuration = kit.LegStepCooldown;
                robot.numberOfAttacksInChain = kit.AttacksInAChain;
                robot.chainedDelayBetweenAttacks = kit.DelayBetweenChainedAttacks;
            }
            else
            {
                RemoveComponentIfPresent<RobotBossAuthoring>(root);
            }

            if (kit.FightsLikeTheOctopus)
            {
                OctopusBossAuthoring octopus = EnsureComponent<OctopusBossAuthoring>(root);
                octopus.appearDuration = kit.OctopusAppearSeconds;
                octopus.durationBeforeStartingToSpawnTentacles =
                    kit.OctopusSecondsBeforeTentacles;
                octopus.durationBeforeLeaveTentacleSpawnState =
                    kit.OctopusSecondsBeforeStopping;
                octopus.tentacleSpawn = new OctopusBossAuthoring.SpawnConfiguration
                {
                    durationUntilSpawn = kit.TentacleWindUp,
                    durationAfterSpawn = kit.TentacleRecovery,
                    minCooldown = kit.TentacleMinCooldown,
                    maxCooldown = kit.TentacleMaxCooldown
                };
            }
            else
            {
                RemoveComponentIfPresent<OctopusBossAuthoring>(root);
            }

            if (kit.FightsLikeTheLarva)
            {
                BossLarvaAuthoring larva = EnsureComponent<BossLarvaAuthoring>(root);
                larva.damage = kit.LarvaDamage;
                larva.damageMultiplier = kit.LarvaMultiplier;
                larva.segmentPrefabSmall = kit.LarvaSmallSegment;
                larva.segmentPrefabMedium = kit.LarvaMediumSegment;
                larva.segmentPrefabLarge = kit.LarvaLargeSegment;

                // The roam numbers differ between a classic world and a full-release one, so the
                // game stores each as a pair. One authored number is written to both, because
                // "roams 30 tiles" means the same thing to a person whichever world they are in.
                larva.roamDistance = new WorldGenerationTypeDependentValue<int>
                {
                    classic = kit.LarvaRoamDistance,
                    fullRelease = kit.LarvaRoamDistance
                };
                larva.roamDeviation = new WorldGenerationTypeDependentValue<int>
                {
                    classic = kit.LarvaRoamVariation,
                    fullRelease = kit.LarvaRoamVariation
                };
            }
            else
            {
                RemoveComponentIfPresent<BossLarvaAuthoring>(root);
            }

            if (kit.FightsLikeTheShaman)
            {
                ShamanBossAuthoring shaman = EnsureComponent<ShamanBossAuthoring>(root);
                shaman.phase1HealthThreshold = kit.ShamanPhaseAtHealth;
                shaman.phase1TransitionDuration = kit.ShamanPhaseSeconds;
                shaman.invulnerableDuration = kit.ShamanUntouchableSeconds;
            }
            else
            {
                RemoveComponentIfPresent<ShamanBossAuthoring>(root);
            }

            if (kit.FightsLikeTheSnake)
            {
                EnsureComponent<SnakeBossAuthoring>(root).defeatSoundEffectDelay =
                    kit.SnakeDefeatSoundDelay;
            }
            else
            {
                RemoveComponentIfPresent<SnakeBossAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (kit.LarvaHasNoBody)
            {
                report(
                    "fights like the boss Larva with none of its three body segments set, so it " +
                    "will be a head with nothing behind it.");
            }

            if (kit.BirdStopsBeforeItStarts)
            {
                report(
                    "stops dropping stones before it starts dropping them, so it never drops any.");
            }
        }

        public static void ApplyCoreBossKit(
            GameObject root,
            DimensionCoreBossKitTemplate kit,
            System.Action<string> report)
        {
            if (root == null || kit == null)
            {
                return;
            }

            Toggle<CoreBossOrbAuthoring>(root, kit.IsOneOfTheCoresOrbs);
            Toggle<TheCoreAuthoring>(root, kit.IsTheCoreItself);
            Toggle<CoreAttentionMarkerAuthoring>(root, kit.IsTheAttentionMarker);
            Toggle<WallBossHeadAuthoring>(root, kit.IsTheWallsHead);

            if (report != null && kit.IsTheAttentionMarker)
            {
                report(
                    "is the marker that points at the Core, which the game only draws for the Core " +
                    "boss and the crystal meteor, so on anything else nothing acts on it.");
            }

            if (kit.FightsLikeTheCore)
            {
                CoreBossAuthoring core = EnsureComponent<CoreBossAuthoring>(root);
                core.orbCount = kit.OrbCount;
                core.orbRotationSpeed = kit.OrbSpeed;
                core.orbMinDistance = kit.OrbMinDistance;
                core.orbMaxDistance = kit.OrbMaxDistance;
                core.phase1HealthThreshold = kit.PhaseChangesAtHealth;
                core.phase1TransitionDuration = kit.PhaseChangeSeconds;
                core.invulnerableDuration = kit.UntouchableForSeconds;
                core.whirlwindProjectileDamage = kit.WhirlwindDamage;
                core.whirlwindProjectileDamageMultiplier = kit.WhirlwindMultiplier;
                core.homingTriangleProjectileDamage = kit.HomingDamage;
                core.homingTriangleProjectileDamageMultiplier = kit.HomingMultiplier;

                core.voidSpawn = new CoreBossAuthoring.VoidSpawnConfiguration
                {
                    disabled = !kit.SummonsVoid,
                    duration = kit.VoidSummonSeconds,
                    durationUntilSpawn = kit.VoidSummonWindUp,
                    durationAfterSpawn = kit.VoidSummonRecovery,
                    minCooldown = kit.VoidSummonMinCooldown,
                    maxCooldown = kit.VoidSummonMaxCooldown
                };

                core.beamSpawn = new CoreBossAuthoring.SpawnConfiguration
                {
                    disabled = !kit.SummonsBeams,
                    durationUntilSpawn = kit.BeamSummonWindUp,
                    durationAfterSpawn = kit.BeamSummonRecovery,
                    minCooldown = kit.BeamSummonMinCooldown,
                    maxCooldown = kit.BeamSummonMaxCooldown
                };
            }
            else
            {
                RemoveComponentIfPresent<CoreBossAuthoring>(root);
            }

            if (kit.FightsLikeTheWall && !kit.WallHasNoSegments)
            {
                WallBossAuthoring wall = EnsureComponent<WallBossAuthoring>(root);
                wall.distanceFromCore = kit.DistanceFromTheCore;
                wall.totalSegments = kit.Segments;
                wall.segmentRadius = kit.SegmentRadius;
                wall.totalWidth = kit.TotalWidth;
                wall.attackDuration = kit.WallAttackSeconds;
                wall.attackCooldown = kit.WallAttackCooldown;
                wall.slitheringFrequencyMultiplier = kit.SlitherSpeed;
                wall.slitheringWavelengthMultiplier = kit.SlitherWavelength;
                wall.slitheringWaveHeightMultiplier = kit.SlitherHeight;
                wall.pauseBeforeBulbsEmergeDuration = kit.PauseBeforeBulbs;
                wall.pauseBeforeHeadEmergesDuration = kit.PauseBeforeHead;
                wall.vulnerableDuration = kit.WallVulnerableSeconds;
                wall.vulnerableOnDamageMaxDuration = kit.WallVulnerableCutShort;
                wall.headOffset = kit.HeadOffset;
                wall.bulbOffset = kit.BulbOffset;

                wall.movement = new System.Collections.Generic.List<MovementParameters>();
                DimensionWallMovement[] moves = kit.MovementByPlayersAlive;
                for (int i = 0; i < moves.Length; i++)
                {
                    wall.movement.Add(new MovementParameters
                    {
                        onTotalAliveTargets = moves[i].WhenThisManyAlive,
                        maxSpeed = moves[i].TopSpeed,
                        accelerationSpeed = moves[i].Acceleration,
                        decelerationSpeed = moves[i].Deceleration,
                        decelerationDurationOnEnter = moves[i].SlowingSecondsOnEntering
                    });
                }
            }
            else
            {
                RemoveComponentIfPresent<WallBossAuthoring>(root);
            }

            if (kit.FightsLikeTheScarab)
            {
                ScarabBossAuthoring scarab = EnsureComponent<ScarabBossAuthoring>(root);
                scarab.appearDuration = kit.ScarabAppearSeconds;
                scarab.buryDuration = kit.ScarabBurySeconds;
                scarab.unearthDuration = kit.ScarabSurfaceSeconds;
                scarab.minChargeCooldown = kit.ScarabMinChargeCooldown;
                scarab.maxChargeCooldown = kit.ScarabMaxChargeCooldown;
                scarab.chargeDamage = kit.ScarabChargeDamage;
                scarab.chargeDamageMultiplier = kit.ScarabChargeMultiplier;
                scarab.bombScarabSpawnAnticipationDuration = kit.BombScarabWindUp;
                scarab.bombScarabSpawnDuration = kit.BombScarabSeconds;
                scarab.bombScarabSpawnEndDuration = kit.BombScarabRecovery;
                scarab.bombScarabSpawnMinCooldown = kit.BombScarabMinCooldown;
                scarab.bombScarabSpawnMaxCooldown = kit.BombScarabMaxCooldown;
            }
            else
            {
                RemoveComponentIfPresent<ScarabBossAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (kit.PhaseThresholdMakesNoFight)
            {
                report(
                    "changes phase at full or empty health, so it either changes gear the instant " +
                    "the fight starts or never changes at all. Vanilla uses halfway.");
            }

            if (kit.WallHasNoSegments)
            {
                report("fights like the Wall with no segments, so there is no wall to fight.");
            }
        }

        public static void ApplyBorrowedBossKit(
            GameObject root,
            DimensionBorrowedBossKitTemplate kit,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || kit == null)
            {
                return;
            }

            if (kit.FightsLikeAHydra)
            {
                HydraBossAuthoring hydra = EnsureComponent<HydraBossAuthoring>(root);
                hydra.hydraType = (HydraBossType)(int)kit.HydraKind;
                hydra.vulnerableEntityPrefab = kit.WeakPointPrefab;

                hydra.buryDuration = kit.BuryingSeconds;
                hydra.unearthDuration = kit.SurfacingSeconds;
                hydra.buriedMinCooldown = kit.MinSecondsUnderground;
                hydra.buriedMaxCooldown = kit.MaxSecondsUnderground;

                hydra.buriedAppearDamage = kit.SurfacingSlamDamage;
                hydra.buriedAppearDamageMultiplier = kit.SurfacingSlamMultiplier;
                hydra.beamDamage = kit.BeamDamage;
                hydra.beamDamageMultiplier = kit.BeamMultiplier;
                hydra.stalactiteMortarDamage = kit.StalactiteDamage;
                hydra.stalactiteMortarDamageMultiplier = kit.StalactiteMultiplier;
                hydra.shockwaveDamage = kit.ShockwaveDamage;
                hydra.shockwaveDamageMultiplier = kit.ShockwaveMultiplier;
                hydra.iceShardMortarDamage = kit.IceShardDamage;
                hydra.iceShardMortarDamageMultiplier = kit.IceShardMultiplier;
                hydra.lavaMortarDamage = kit.LavaDamage;
                hydra.lavaMortarDamageMultiplier = kit.LavaMultiplier;
                hydra.nilipedeMortarDamage = kit.NilipedeDamage;
                hydra.nilipedeMortarDamageMultiplier = kit.NilipedeMultiplier;
            }
            else
            {
                RemoveComponentIfPresent<HydraBossAuthoring>(root);
            }

            Toggle<SlimeBossAuthoring>(root, kit.CyclesItsShotsLikeTheSlimeKing);

            if (report != null && kit.CyclesItsShotsLikeTheSlimeKing)
            {
                report(
                    "cycles its shots like the Slime King, which the game only ever does for the " +
                    "Lava Slime Boss itself, so the setting is carried but nothing acts on it.");
            }

            if (kit.SlamsLikeTheSlime)
            {
                SlimeBossJumpStateAuthoring slam =
                    EnsureComponent<SlimeBossJumpStateAuthoring>(root);
                slam.anticipationTime = kit.SlamWindUp;
                slam.maxAirTime = kit.SlamAirTime;
                slam.landTime = kit.SlamLandTime;
                slam.jumpMoveSpeed = kit.SlamSpeed;
                slam.enragedAnticipationTime = kit.EnragedSlamWindUp;
                slam.enragedMaxAirTime = kit.EnragedSlamAirTime;
                slam.enragedJumpMoveSpeed = kit.EnragedSlamSpeed;
                slam.damage = kit.SlamDamage;
                slam.damageMultiplier = kit.SlamMultiplier;

                if (!string.IsNullOrEmpty(kit.SlamLeavesTilesetId))
                {
                    int left = ResolveTilesetName(kit.SlamLeavesTilesetId, resolveTileset);
                    if (left >= 0)
                    {
                        slam.slimeTileset = (PugTilemap.Tileset)left;
                    }
                    else if (report != null)
                    {
                        report(
                            "leaves '" + kit.SlamLeavesTilesetId + "' where it slams, which is not " +
                            "a tileset, so it leaves the ground as it was.");
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<SlimeBossJumpStateAuthoring>(root);
            }

            if (kit.EnragingMakesItLessDangerous && report != null)
            {
                report(
                    "gets SLOWER when it enrages — its enraged wind-up is longer or its enraged " +
                    "leap slower than its ordinary one. The fight will get easier exactly where a " +
                    "player expects it to get harder.");
            }
        }
    }
}
