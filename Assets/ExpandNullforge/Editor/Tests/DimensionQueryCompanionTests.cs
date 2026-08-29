#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Stops the framework writing one component of a query and none of the others.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE FAILURE THIS GUARDS. A Core Keeper system only touches an entity carrying EVERY
    /// component its query names. The framework writes the one component that obviously belongs to
    /// a feature, and the others — which come from unrelated authoring components — go unwritten.
    /// The entity never matches, the system never runs, and nothing reports anything: the component
    /// we wrote is present, its converter ran, and the number in it is right. It was found by hand
    /// once, on the summoning circle, and a census then found a hundred more.
    /// </para>
    /// <para>
    /// WHAT THIS TEST DOES NOT DO, said plainly. It does not read Core Keeper's queries, and it
    /// does not read Core Keeper's own prefabs. Both are readable — the decompiled systems at
    /// <c>E:\ck mods\ck-db</c> are plain C# with the <c>WithAll</c> chains intact, and the game's
    /// prefabs are on disk — but they are outside this repository and outside anything this
    /// assembly references, so reading them is a person's job done against
    /// <c>E:\ck mods\ck-research\query-match-census.md</c> rather than a thing the build can check.
    /// An earlier version of this remark said the queries were unrecoverable. That was wrong, and
    /// it made a backlog of two hundred unread components look like a tooling limit instead of
    /// work nobody had done yet.
    /// </para>
    /// <para>
    /// WHAT IT DOES INSTEAD. Three things, and they cover the reintroduction path rather than the
    /// discovery path:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// It reads every framework source file — whole, not line by line, so a call wrapped across two
    /// lines is still seen — and fails when a Core Keeper surface it puts on something is answered
    /// by none of the four lists. "Core Keeper surface" is decided by
    /// <see cref="NotACoreKeeperSurface"/>, a written-down list of the things that are NOT one,
    /// rather than by a suffix: an earlier version dropped every name not ending in "Authoring"
    /// before consulting any list, which quietly excused <c>InteractableObject</c> and seven
    /// runtime components added straight to entities.
    /// </description></item>
    /// <item><description>
    /// It builds an object carrying each covered component, runs the PRODUCTION sweep over it, and
    /// then READS the object without touching it — and fails when a companion is still missing and
    /// either the row claimed to supply it or nothing was said. It also counts what the sweep
    /// closed and fails at zero, so an empty sweep cannot pass. Until this pass the reading step
    /// performed every fill itself, and emptying the sweep left the test green.
    /// </description></item>
    /// <item><description>
    /// It pins the two behaviours the sweep depends on: that seeing nearby things merges rather
    /// than overwrites, and that a thing which moves is allowed to turn.
    /// </description></item>
    /// </list>
    /// <para>
    /// SO THE GAP IT LEAVES IS DISCOVERY. It used to be two hundred components wide. Four more
    /// censuses on 2026-08-29 read 186 of those two hundred against the system that consumes
    /// them, and those names moved into
    /// <see cref="ReadAgainstTheirSystemAndTheVerdictIsRecorded"/>.
    /// <see cref="NobodyHasReadTheSystemThatConsumesTheseYet"/> is what NOBODY HAS OPENED AT ALL.
    /// It is not the whole of what is unresolved: the recorded-verdict list also holds names whose
    /// verdict is UNCERTAIN — the census marks <c>WallBossAuthoring</c>,
    /// <c>DetectCollisionAuthoring</c>, <c>CanClaimBedAuthoring</c>, <c>CoinAmountAuthoring</c> and
    /// <c>AffixAuthoring</c> that way — so a name there can mean read and not concluded. Read that
    /// list's own remark, and the census, before assuming anything.
    /// </para>
    /// <para>
    /// It also does not check that the component we add is the one the system wants, only that
    /// somebody looked, and — for the covered rows — that what the sweep promises to supply is
    /// actually there afterwards. Whether a recorded verdict is still true after the game updates
    /// is a person's job, done against `E:\ck mods\ck-research\query-match-census.md`, which
    /// <see cref="EveryNameSaidToHaveAVerdictHasOneInTheCensus"/> now at least opens.
    /// </para>
    /// <para>
    /// WHAT IT STILL CANNOT SEE, written down so nobody has to rediscover it. (1) A component that
    /// arrives on a LOADED prefab: fifteen generators regenerate in place through
    /// <c>PrefabUtility.LoadPrefabContents</c> and nothing strips what an older pass wrote, so a
    /// component the framework wrote in one release and dropped in the next is still on every
    /// user's prefab, still in the query, and the staleness tests here have since deleted the
    /// record that anybody read it. Closing that means reading generated prefabs, which are outside
    /// this assembly. (2) Answers are keyed by component NAME, so moving an already-answered name
    /// into a different generator passes. (3) The sweep check is one boolean per FILE: a file that
    /// sweeps object A and writes surfaces on object B passes, and a dead private method named like
    /// the sweep would satisfy it. Blanking the string literals closed the version of that hole
    /// that a warning sentence could walk through; the dead-method version is still open.
    /// </para>
    /// </remarks>
    internal sealed class DimensionQueryCompanionTests
    {
        /// <summary>
        /// Components the generators add whose systems need nothing else beside them, read against
        /// the query and recorded so the sweep does not have to guess.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A name here is a claim that somebody read the system that consumes the component and
        /// found its query satisfied by what the framework already writes. Adding a name without
        /// doing that is how the bug comes back.
        /// </para>
        /// <para>
        /// AND IT IS THE ONE LIST NOTHING AUDITS, which is worth knowing before adding to it.
        /// <see cref="EveryNameSaidToHaveAVerdictHasOneInTheCensus"/> opens the census and checks
        /// every name in the OTHER approval list; it cannot check this one, because nineteen of the
        /// names here have no verdict written into the census at all, so the test would be red the
        /// day it was written rather than red the day somebody cheats. That makes this the cheapest
        /// way to make a name go away: delete its companion row and put the name here, and the
        /// build stays green. Until the census carries all of them, the only thing standing behind
        /// a name here is whoever typed it.
        /// </para>
        /// <para>
        /// THE NINETEEN, so that writing their verdicts is a job somebody can pick up:
        /// <c>PotionAuthoring</c>, <c>OffHandAuthoring</c>, <c>CookedFoodAuthoring</c>,
        /// <c>EquipmentSkinAuthoring</c>, <c>TitanShrineAuthoring</c>,
        /// <c>SecondaryUseAuthoring</c>, <c>TeleportStateAuthoring</c>,
        /// <c>EnrageStateAuthoring</c>, <c>CombatRadiusAuthoring</c>,
        /// <c>ConsumesManaAuthoring</c>, <c>CustomAttackSoundAuthoring</c>,
        /// <c>BossSpawnLocationAuthoring</c>, <c>CanBeRemovedByWaterAuthoring</c>,
        /// <c>ChangeVariationWhenContainingObjectAuthoring</c>,
        /// <c>AffectObjectWhenMelodyPlayedAuthoring</c>,
        /// <c>EnsureSameGroundTileBeneathEntityAuthoring</c>,
        /// <c>GivesConditionsWhenConsumedAuthoring</c>,
        /// <c>HatchWhenPlayerNearbyStateAuthoring</c> and <c>IgnoreVertexOffsetsAuthoring</c>.
        /// They are deliberately not written into the census as a list, because that check is a
        /// substring match and putting the names there would make the promise true by spelling
        /// rather than by reading.
        /// </para>
        /// <para>
        /// NOT ALL OF THEM ARE CORE KEEPER'S. <c>PhysicsShapeAuthoring</c> and
        /// <c>PhysicsBodyAuthoring</c> are Unity Physics and <c>GhostAuthoringComponent</c> is
        /// NetCode. They are here because the scan reads them off the same helpers, and the reading
        /// behind them is the same reading — but the summary above used to call every name in the
        /// list a Core Keeper component, and three of them are not.
        /// </para>
        /// <para>
        /// A NAME WITH A COMPANION ROW MAY NOT BE IN HERE, and 25 of the 26 row names used to be.
        /// The two say opposite things — a row exists because that component's system needs
        /// something beside it — and while both were consulted, deleting a row left the build green
        /// and the feature silently broken, which is the exact failure this file was written for.
        /// <see cref="NoNameSitsInMoreThanOneOfTheAnswerLists"/> makes it impossible to do again.
        /// </para>
        /// </remarks>
        private static readonly HashSet<string> ReadAgainstTheirSystemAndNeedNothingBeside =
            new HashSet<string>(StringComparer.Ordinal)
            {
                // The universal spine, on essentially every generated object.
                "ObjectAuthoring", "AnimationAuthoring", "IgnoreVertexOffsetsAuthoring",
                "StateAuthoring", "IdleStateAuthoring", "TookDamageStateAuthoring",
                "DeathStateAuthoring", "AreaLevelAuthoring", "HealthAuthoring",
                "DamageReductionAuthoring", "InventoryItemAuthoring", "PlaceableObjectAuthoring",
                "RotationAuthoring", "PaintableObjectAuthoring", "DescriptionAuthoring",
                "SupportsConditionsAuthoring", "RandomWalkGravityAuthoring",

                // Read by lookup rather than by query: no query to satisfy.
                "MeleeWeaponAuthoring", "RangeWeaponAuthoring", "CastItemAuthoring",
                "BeamWeaponAuthoring", "DurabilityAuthoring", "InstrumentAuthoring",
                "EquipmentSkinAuthoring", "CustomAttackSoundAuthoring", "OffHandAuthoring",
                "ConsumesManaAuthoring", "FullnessAuthoring",
                "CookingIngredientAuthoring", "CookedFoodAuthoring", "FlowerAuthoring",
                "FishAuthoring", "PotionAuthoring", "TrophyAuthoring", "ExtractableAuthoring",
                "SecondaryUseAuthoring", "GivesConditionsWhenEquippedAuthoring",
                "GivesConditionsWhenConsumedAuthoring", "WeaponDamageAuthoring",
                "ToggleInteractionOnVariationAuthoring", "SurfacePriorityAuthoring",
                "MineableAuthoring", "DiggableAuthoring", "CantBeAttackedAuthoring",
                "IndestructibleAuthoring", "DontDropSelfAuthoring", "DontSerializeAuthoring",
                "CanBePickedUpAuthoring", "AlwaysDropVariationZeroAuthoring",
                "ImmuneToRangeDamageAuthoring", "ImmuneToSkipLootDropAuthoring",
                "CanBeRemovedByWaterAuthoring", "DestroyIfNotOnTileAuthoring",

                // Read against their query and satisfied by what the spine already writes.
                "DropLootAuthoring", "DestroyTimerAuthoring",
                "SpawnCompanionsAuthoring", "AdaptiveEntityBufferAuthoring",
                "ScaleHealthByPlayerCountAuthoring", "DestroyWhenNoNearbyPlayerAuthoring",
                "DestroyNearbyOnDeathAuthoring", "RoamingPathAuthoring", "HasSpawnPointAuthoring",
                "SpawnAroundObjectAuthoring", "MapMarkerAuthoring", "CanBeDiscoveredAuthoring",
                "CanBeScannedAuthoring", "CustomDisableAuthoring",
                "OverrideNetworkSyncDistanceAuthoring", "ImmunityZoneAuthoring",
                "MusicAreaAuthoring", "BossSpawnLocationAuthoring", "TitanShrineAuthoring",
                "EnsureSameGroundTileBeneathEntityAuthoring", "PseudoTileAuthoring",
                "SpawnTileOnDeathAuthoring", "RemoveTileOnDeathAuthoring",
                "TileEffectAuthoring",
                "AffectObjectWhenMelodyPlayedAuthoring", "BaitOnAPoleAuthoring",
                "ChangeVariationWhenContainingObjectAuthoring", "CraftingAuthoring",
                "InventoryAuthoring", "OccupiableAuthoring", "BoatAuthoring", "MinecartAuthoring",
                "VehicleAuthoring", "CanBeControlledByOtherEntityAuthoring", "CritterAuthoring",
                "MovementSpeedAuthoring", "FactionAuthoring", "BehaviourTagsAuthoring",
                "NearbyEntitiesTrackerAuthoring", "CombatRadiusAuthoring", "EnemyAuthoring",
                "BossAuthoring", "ChaseStateAuthoring", "MeleeAttackStateAuthoring",
                "RangeAttackStateAuthoring", "RandomWalkStateAuthoring", "EnrageStateAuthoring",
                "ExplodeStateAuthoring", "TeleportStateAuthoring", "IdleInCombatStateAuthoring",
                "SpawnStateAuthoring", "HatchWhenPlayerNearbyStateAuthoring",
                "PlaceObjectStateAuthoring", "ProjectileAuthoring", "MortarProjectileAuthoring",
                "ExplosionAuthoring", "ExplosiveAuthoring", "InteractWithEnvironmentAuthoring",
                "ElectricityAuthoring", "AncientElectricityConnectionAuthoring",
                "DirectionBasedOnVariationAuthoring", "ChangeVariationTriggerAuthoring",
                "PortalAuthoring", "RootPlantAuthoring",
                "GhostAuthoringComponent", "PhysicsShapeAuthoring", "PhysicsBodyAuthoring",
                "LocalInteractableAuthoring", "InteractableObject",

                // Put straight onto the entity by the framework's own beam converter, and read
                // against its systems: every query in the game that names AttackCooldownTimerCD
                // names its own attack state beside it — melee at MeleeAttackStateSystem.cs:793,
                // jump at JumpAttackStateSystem.cs:645, the beam at BeamAttackStateSystem.cs:217 —
                // so adding it pulls nothing into a query it does not belong in, and it needs
                // nothing beside itself.
                "AttackCooldownTimerCD",
            };

        /// <summary>
        /// Core Keeper authoring components the generators add that HAVE been read against the
        /// system that consumes them, with the verdict written down — which is not the same as
        /// saying they all work.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHAT A NAME HERE CLAIMS, exactly. Somebody opened the system that reads this component
        /// in the decompiled game, read its query — every <c>WithAll</c>, <c>WithNone</c>,
        /// <c>WithAllRW</c> and <c>RequireForUpdate</c> — read what the converter does and does not
        /// add beside it, and diffed the object the framework builds against Core Keeper's own
        /// prefab for the same job. The result of that read is written up by name in
        /// `E:\ck mods\ck-research\query-match-census.md`, under the census that answered it.
        /// </para>
        /// <para>
        /// WHAT A NAME HERE DOES NOT CLAIM. It does not claim the feature works. FIVE kinds of
        /// verdict live in this list side by side: the query is already satisfied; the query is not
        /// satisfied and the fix is in this tree; the component is read only off a different kind
        /// of entity — the player, an equipped item, a projectile — so the answer cannot work where
        /// it is offered; the component is a dead end in the shipped game, read by nothing at
        /// all; and UNCERTAIN — somebody opened the system and did not reach a conclusion.
        /// <c>WallBossAuthoring</c>, <c>DetectCollisionAuthoring</c>, <c>CanClaimBedAuthoring</c>,
        /// <c>CoinAmountAuthoring</c> and <c>AffixAuthoring</c> are in here on that fifth footing,
        /// and an earlier version of this remark listed only the first four, so a reader could not
        /// learn the fifth existed. The census says which, per name. Treating this list as an
        /// approval is how a finding gets lost.
        /// </para>
        /// <para>
        /// It is separate from <see cref="ReadAgainstTheirSystemAndNeedNothingBeside"/> on purpose.
        /// That list is a narrower and stronger claim — read, AND nothing else is needed beside it
        /// — and moving a name into it without that being true is how the bug this file guards
        /// comes back.
        /// </para>
        /// </remarks>
        private static readonly HashSet<string> ReadAgainstTheirSystemAndTheVerdictIsRecorded =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "AchievementTrackerAuthoring",
                "ActAsDestructibleWhileAboveHealthThresholdAuthoring",
                "ActAsLightSourceWhenHeldInHandAuthoring",
                "AffectedByAutomationAuthoring",
                "AffixAuthoring",
                "AlertEmoteStateAuthoring",
                "AlwaysDropOneAuthoring",
                "AnimationSpeedAuthoring",
                "AnvilAuthoring",
                "AttackableWithMeleeAuthoring",
                "AuraDistanceOverrideAuthoring",
                "AutomatedApplyFilterForMoversAuthoring",
                "AutomatedCrafterAuthoring",
                "AutomatedHarvestAndMoverAuthoring",
                "AutomatedMinerAuthoring",
                "AutomatedMoveAndPlanterAuthoring",
                "AutomatedMoverAuthoring",
                "AutomatedMoverSharedAuthoring",
                "AutomatedStorageAuthoring",
                "BeDestroyedAlongWithOwnerAuthoring",
                "BehaviourAuthoring",
                "BirdBossAuthoring",
                "BirdBossBeamAuthoring",
                "BossLarvaAuthoring",
                "BossLarvaSpawnStateAuthoring",
                // "BreedStateAuthoring" moved OUT of this list and into two companion rows:
                // breeding needs a chase to walk to a mate with and an eat state to count
                // meals against, and a passive animal was given neither.
                "BreedToggleAuthoring",
                "CanClaimBedAuthoring",
                "CantBeSoldAuthoring",
                "ChanceToApplyConditionToSelfWhenDamagedAuthoring",
                "ChangeVariationAfterTimeAuthoring",
                "ChangeVariationWhenTookDamageAuthoring",
                "ChargeAttackStateAuthoring",
                "CherryBlossomTreeAuthoring",
                "CicadaBossAuthoring",
                "CicadaNymphAuthoring",
                "ClientBiomeSamplesAuthoring",
                "ClientSubMapAuthoring",
                "CoinAmountAuthoring",
                "CombatEmoteStateAuthoring",
                "CombatantsTrackerAuthoring",
                "CommandMinionWeaponAuthoring",
                "ContainedMiniSimAuthoring",
                "ContainedMiniSimElementAuthoring",
                "ConvertToInterpolatedGhostAfterSpawnAuthoring",
                "ConvertToTileAuthoring",
                "CooldownAuthoring",
                "CoreAttentionMarkerAuthoring",
                "CoreBossAuthoring",
                "CoreBossBeamAuthoring",
                "CoreBossOrbAuthoring",
                "CoreBossSpawnAuthoring",
                "CornerSmoothingAuthoring",
                "CrackableTileAuthoring",
                "CreateCharacterGuidAuthoring",
                "CreatePlayerGuidAuthoring",
                "CritterCatcherCatchableAuthoring",
                "CustomScenePrefabAuthoring",
                "DamageEffectAuthoring",
                "DamageObjectStateAuthoring",
                "DamageableObjectAuthoring",
                "DestroyEntityIfPlacementNotValidAuthoring",
                "DestructibleObjectAuthoring",
                "DetectCollisionAuthoring",
                "DisableImmuneZoneAuthoring",
                "DisableMapMarkerOnDeathAuthoring",
                "DisablePhysicsAuthoring",
                "DisplayConditionAsBarWhenEquippedAuthoring",
                "DontBlockDiggingAuthoring",
                "DontCountAsHitForAttackerAuthoring",
                "DontDropContainedAuthoring",
                "DontNeedTransformAuthoring",
                "DrillAuthoring",
                "DropAllItemsOnHitAuthoring",
                "EatStateAuthoring",
                "EntityPartAuthoring",
                "EquipmentAuthoring",
                "EvolveStateAuthoring",
                "ExplodeOnImpactAuthoring",
                "ExtraInventorySizeAuthoring",
                "FenceGateAuthoring",
                "FireSpreaderAuthoring",
                "FireflyAuthoring",
                "FishShoalAuthoring",
                "FishingNetVisualAuthoring",
                "FollowPheromoneStateAuthoring",
                "ForceInCombatIfPlayerNearbySpawnPointAuthoring",
                "GiantCicadaBossAuthoring",
                "GlowLightAuthoring",
                "GroundBouncableProjectileAuthoring",
                "GroundDecorationAuthoring",
                "GrowingPlantAuthoring",
                "HealOtherEntityStateAuthoring",
                "HydraBossAuthoring",
                "HydraBossBaitAuthoring",
                "IdleEmoteStateAuthoring",
                "IdleWhenNearbyPlayerStateAuthoring",
                "IgnitableAuthoring",
                "IgnoreImmuneZoneAuthoring",
                "ImmuneToDamageAuthoring",
                "ImmuneToPushBackAuthoring",
                "IndirectProjectileAuthoring",
                "IsFlyingAuthoring",
                "IsHabitableIdolAuthoring",
                "JewelryAuthoring",
                "JumpAttackStateAuthoring",
                "LarvaHiveBossHatchEggStateAuthoring",
                "LarvaHiveEggHatchStateAuthoring",
                "LeaveTrailAuthoring",
                "LocalizationAuthoring",
                "ManaAuthoring",
                "ManaBarrierAuthoring",
                "MealsEatenAuthoring",
                "MergeDroppedItemAuthoring",
                "MimicPlayerInstrumentNotesAuthoring",
                "MinionAuthoring",
                "MinionDataAuthoring",
                "MotionSmoothingAuthoring",
                "MoveToPositionFromCommandStateAuthoring",
                "MusicSheetAuthoring",
                "NameAuthoring",
                "NonHittableAuthoring",
                "OctopusBossAuthoring",
                "OctopusBossTeleportLocationAuthoring",
                "OverrideLeaveCombatTimeAuthoring",
                "OverrideLegendaryForSlotRequirementsAuthoring",
                "OwnerAuthoring",
                "PaintToolAuthoring",
                "PetAuthoring",
                "PetCandyAuthoring",
                "PetDataAuthoring",
                "PetOwnerAuthoring",
                "PetWalkStateAuthoring",
                "PheromoneAdderAuthoring",
                "PheromoneSensorAuthoring",
                "PlacementIndicatorAuthoring",
                "PlayerGraveAuthoring",
                "PlayingInstrumentAuthoring",
                "PrioritizedRepairMaterialAuthoring",
                "PugDamageAuthoring",
                "PutTargetInCombatOnDealingDamageAuthoring",
                "RandomFollowStateAuthoring",
                "RandomWalkGravityWellAuthoring",
                "RayAttackStateAuthoring",
                "RecipeAuthoring",
                "ResizableTileSizeAuthoring",
                // "RoamingStateAuthoring" moved OUT of this list and into a companion row: the
                // roaming system names RoamingPathBuffer and only the patrol route produces one.
                "RobotBossAuthoring",
                "ScannerAuthoring",
                "ScarabBossAuthoring",
                "SeasonObjectAuthoring",
                "SellSlotsAuthoring",
                "ShamanBossAuthoring",
                "ShieldAuthoring",
                "ShootMortarProjectileStateAuthoring",
                "SleepStateAuthoring",
                "SlimeBossAuthoring",
                "SlimeBossJumpStateAuthoring",
                "SnakeBossAuthoring",
                "SnakeMovementStateAuthoring",
                "SoulOrbAuthoring",
                "SoulsAuthoring",
                "SpawnDroppedItemAuthoring",
                "SpawnOnDeathAuthoring",
                "SprinklerAuthoring",
                "SupportAffixesAuthoring",
                "TableItemLightSourceAuthoring",
                "TheCoreAuthoring",
                "TileAuthoring",
                "TrailAuthoring",
                "TrashCanAuthoring",
                "TriggerAchievementOnDeathAuthoring",
                "TriggerEffectAuthoring",
                "TriggerEnvironmentEventOnDeathAuthoring",
                "UpgradeSlotAuthoring",
                "VanitySlotsAuthoring",
                "VendingMachineAuthoring",
                "VisualSmoothFollowAuthoring",
                "VulnerableStateAuthoring",
                "WallBossAuthoring",
                "WallBossHeadAuthoring",
                "WarmupAuthoring",
                "WaterSourceAuthoring",
                "WaterSpreaderAuthoring",
                "WeaponSkillGainedMultiplierAuthoring",
                "WorldExplorerDebugTrackerAuthoring",
            };

        /// <summary>
        /// The Core Keeper surfaces the framework writes whose system NOBODY HAS READ YET.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS LIST IS A BACKLOG, NOT AN APPROVAL. A name here says only that the question has not
        /// been asked: nobody has opened the system that consumes the component, read its query,
        /// and written down what else that query names. Any of them could be a live instance of
        /// exactly the bug this file guards — a feature that generates cleanly, converts cleanly,
        /// and does nothing.
        /// </para>
        /// <para>
        /// WHAT IS LEFT, and why. Four territory spawners
        /// (<c>CavelingTerritorySpawnerAuthoring</c>, <c>CavelingNatureTerritorySpawnerAuthoring</c>,
        /// <c>LarvaTerritorySpawnerAuthoring</c>, <c>SlimeTerritorySpawnerAuthoring</c>) were read
        /// once by the first census against the spawner systems but not against the collision-world
        /// half of those systems, so they are held back rather than promoted. Four automation
        /// answers on plants and ore (<c>AutomatedHarvestablePlantAuthoring</c>,
        /// <c>AutomatedPlantableSeedAuthoring</c>, <c>AutomatedMineableAuthoring</c>) and the two
        /// mini-sim movement answers (<c>AquariumFishMovementAuthoring</c>,
        /// <c>TerrariumCritterMovementAuthoring</c>) sit on the plant and critter generators, which
        /// no census covered end to end. <c>CattleAuthoring</c>, <c>RootAuthoring</c>,
        /// <c>MoveFreelyWeaponAuthoring</c>, <c>SummoningItemAuthoring</c> and
        /// <c>PugWorldGenAuthoring</c> are one-off surfaces nobody has reached.
        /// </para>
        /// <para>
        /// SEVEN MORE JOINED THEM, and they are not authoring components at all. The framework also
        /// puts Core Keeper's own runtime components straight onto an entity —
        /// <c>DungeonGenerationInitializationCD</c> and <c>BlockSaveCD</c> in
        /// <c>DimensionDungeonAssembler</c>, <c>DontSerializeCD</c> and <c>DontDisableCD</c> in the
        /// runtime loader, <c>IndestructibleCD</c>, <c>DontDropSelfCD</c> and <c>DontDropLootCD</c>
        /// on the portal path. That road skips the converter that would have added the component's
        /// siblings, so it is MORE exposed to this bug than the authoring road, not less, and until
        /// this pass the scan could not even see it: it dropped every name that did not end in
        /// "Authoring" before consulting any list.
        /// </para>
        /// <para>
        /// It is written down rather than left implicit so that the test can still do its one real
        /// job: a component that is added and appears in NONE of the four lists is new, and fails
        /// the build until somebody answers for it. Each name that moves out of here should move
        /// into <see cref="ReadAgainstTheirSystemAndTheVerdictIsRecorded"/> with its verdict written
        /// into the census, into
        /// <see cref="ReadAgainstTheirSystemAndNeedNothingBeside"/> if the query is satisfied by
        /// what we already write, or into a companion row.
        /// </para>
        /// </remarks>
        private static readonly HashSet<string> NobodyHasReadTheSystemThatConsumesTheseYet =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "AquariumFishMovementAuthoring",
                "AutomatedHarvestablePlantAuthoring",
                "AutomatedMineableAuthoring",
                "AutomatedPlantableSeedAuthoring",
                "CattleAuthoring",
                "CavelingNatureTerritorySpawnerAuthoring",
                "CavelingTerritorySpawnerAuthoring",
                "LarvaTerritorySpawnerAuthoring",
                "MoveFreelyWeaponAuthoring",
                "PugWorldGenAuthoring",
                "RootAuthoring",
                "SlimeTerritorySpawnerAuthoring",
                "SummoningItemAuthoring",
                "TerrariumCritterMovementAuthoring",

                // THE VIEW PATH, which had no guard on it at all until this pass. All four are
                // real Core Keeper types that were sitting on the list of things that are not a
                // Core Keeper surface, so the sprites, the shadow, the damage flash and the name
                // tag were written onto generated prefabs with nobody answering for the systems
                // that read them. Named here rather than approved, because nobody has read those
                // systems' queries yet.
                "Flashable",
                "ObjectNameTag",
                "PugText",
                "SpriteObject",

                // Runtime components put straight onto an entity, bypassing the converters.
                "BlockSaveCD",
                "DontDisableCD",
                "DontDropLootCD",
                "DontDropSelfCD",
                "DontSerializeCD",
                "DungeonGenerationInitializationCD",
                "IndestructibleCD",
            };

        /// <summary>
        /// Type arguments the add helpers take that are not a Core Keeper surface at all.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The scan used to decide this with a suffix test — anything not ending in "Authoring" was
        /// dropped before any list was consulted. That silently excused every Core Keeper component
        /// whose class name has a different shape, and two whole classes of them were live:
        /// <c>InteractableObject</c>, which is the thing a player walks up to and presses a key on,
        /// and seven runtime <c>*CD</c> components added straight to entities. Listing what is NOT
        /// a Core Keeper surface, rather than guessing what is, closes that by construction: a name
        /// that is in neither this list nor an answer list fails the build.
        /// </para>
        /// <para>
        /// <c>T</c> is here because the add helpers are themselves generic and their own
        /// declarations match the same pattern.
        /// </para>
        /// </remarks>
        /// <remarks>
        /// <para>
        /// FOUR OF THESE WERE CORE KEEPER TYPES. <c>Flashable</c> (<c>ck-db/Pug.Other</c>),
        /// <c>ObjectNameTag</c> (<c>ck-db/Pug.Other</c>), <c>PugText</c> (<c>ck-db/Pug.Other</c>)
        /// and <c>SpriteObject</c> (<c>ck-db/PugSprite</c>) sat on the list of things that are not
        /// a Core Keeper surface, which did the exact opposite of what the paragraph above claims
        /// for them. The cost was concrete: the creature and plant VIEW path and the floating text
        /// utility write those four onto generated prefabs, and while they were here those three
        /// files were invisible to every test in this file — not asked to sweep, not on the excused
        /// list, their components never answered for. They have moved to the unread backlog, which
        /// is where a surface nobody has read the system for belongs.
        /// </para>
        /// <para>
        /// What is left is Unity's own (<c>Animator</c>, <c>AudioSource</c>, <c>Camera</c>,
        /// <c>SpriteRenderer</c>), Unity's ECS tag <c>Prefab</c>, and <c>T</c>, which is here
        /// because the add helpers are themselves generic and their own declarations match the
        /// same pattern.
        /// </para>
        /// </remarks>
        private static readonly HashSet<string> NotACoreKeeperSurface =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Animator", "AudioSource", "Camera", "Prefab", "SpriteRenderer", "T",
            };

        /// <summary>
        /// Matches something that puts a component on a generated object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// EVERY HELPER IN THIS TREE THAT ADDS ONE, found by reading the generators rather than
        /// guessing. Six names are declared in the tree, in twenty-one places:
        /// <c>EnsureComponent&lt;T&gt;</c> is declared fourteen times — once in
        /// <c>DimensionGeneratedPrefabUtility</c> and once privately in each of thirteen other
        /// files — <c>ApplyComponent&lt;T&gt;</c> twice (<c>DimensionItemGenerator</c>),
        /// <c>Ensure&lt;T&gt;</c> twice (<c>DimensionQueryCompanions</c>,
        /// <c>DimensionTilesetBlockAuthoring</c>), and <c>Toggle&lt;T&gt;</c>,
        /// <c>Fill&lt;T&gt;</c> and <c>Set&lt;T&gt;</c> once each. Beside those the alternation
        /// carries Unity's own <c>AddComponent&lt;T&gt;</c>, the ECS
        /// <c>AddComponentData&lt;T&gt;</c> — which has no call in the tree yet and is one ECS add
        /// away, since the runtime loader already calls <c>AddComponent&lt;Prefab&gt;</c> on an
        /// entity — the converters' <c>EnsureHasComponent&lt;T&gt;</c>, and
        /// <c>GetOrAddComponent&lt;T&gt;</c> — that last
        /// one has no declaration and no call in the tree today and is carried because it is the
        /// usual name for the same helper.
        /// </para>
        /// <para>
        /// FOURTEEN PRIVATE COPIES OF ONE HELPER is why the shape of this scan matters as much as
        /// its contents: there is no single choke point to instrument, so the guard is a text scan,
        /// and a text scan has to read whole files rather than lines. See
        /// <see cref="ScannedFile"/>.
        /// </para>
        /// <para>
        /// TWO OF THOSE WERE MISSING and both were live: <c>ApplyComponent</c> puts the cooldown,
        /// the damageable and the destructible answers on every item, and <c>Set</c> puts the tile
        /// answers on every custom block. Neither matched, so twenty-two components the generators
        /// really do write were invisible to this test and were never even in the backlog. A
        /// helper added later and left out of this list is the same hole, which is why
        /// <see cref="EveryGenericCallOnAnAuthoringTypeIsAKnownForm"/> now fails on a form nobody
        /// has classified rather than passing over it.
        /// </para>
        /// </remarks>
        /// <remarks>
        /// <para>
        /// THE TYPE ARGUMENT IS READ AS A LIST, and its characters allow every legal way of
        /// writing a name. Five forms slipped past the pattern this replaces, each verified by
        /// construction against the real tree: <c>EnsureComponent&lt;PetAuthoring, Tag&gt;</c>
        /// (two arguments matched neither scan), <c>global::Pug.PetAuthoring</c> (the character
        /// class could not cross <c>::</c>), <c>@PetAuthoring</c> (nor the verbatim <c>@</c>),
        /// <c>AddComponentData&lt;T&gt;</c> (the alternation demanded <c>AddComponent</c> exactly,
        /// while the runtime path already calls <c>AddComponent&lt;Prefab&gt;</c> on an entity),
        /// and an aliased type whose alias does not end in Authoring or CD.
        /// </para>
        /// <para>
        /// <c>EveryTypeArgumentIn</c> splits the list; <c>ScannedFile.Resolve</c> strips
        /// <c>global::</c>, the <c>@</c> and the namespace, and follows a using-alias, and it is
        /// called BEFORE anything decides whether the name is a surface.
        /// </para>
        /// </remarks>
        private static readonly Regex AddsAComponent = new Regex(
            @"\b(?:EnsureComponent|EnsureHasComponent|AddComponentData|AddComponent|" +
            @"ApplyComponent|GetOrAddComponent|Ensure|Toggle|Fill|Set)\s*<\s*" +
            @"([\w\.@:\s,]+?)\s*>",
            RegexOptions.Compiled);

        /// <summary>
        /// Copying a component from one object to another, which names no type any scan can read.
        /// </summary>
        /// <remarks>
        /// <c>ComponentUtility.CopyComponent</c> takes a component instance and
        /// <c>PasteComponentAsNew</c> names nothing at all, so a real Core Keeper component can
        /// land on a generated object with every scan in this file silent. There are none in the
        /// tree today; this refuses the form outright rather than waiting for the first one.
        /// </remarks>
        private static readonly Regex CopiesAComponent = new Regex(
            @"\b(?:CopyComponent|PasteComponentAsNew|PasteComponentValues)\s*\(",
            RegexOptions.Compiled);

        /// <summary>
        /// Matches any generic call whose type argument is a Core Keeper surface.
        /// </summary>
        /// <remarks>
        /// <c>*Authoring</c>, <c>GhostAuthoringComponent</c>, <c>InteractableObject</c> and the
        /// runtime <c>*CD</c> components. It used to be the authoring suffix alone, which left a
        /// new helper used only on a runtime component — <c>Attach&lt;CavelingCD&gt;(entity)</c> —
        /// matching neither this nor the add scan, so it would have shipped with nobody answering
        /// for it. Adding a CD by hand skips the converter that would have added its siblings, so
        /// that road is more exposed to this bug than the authoring one.
        /// </remarks>
        private static readonly Regex AnyGenericCall = new Regex(
            @"\b([A-Za-z_]\w*)\s*<\s*([\w\.@:\s,]+?)\s*>",
            RegexOptions.Compiled);

        /// <summary>
        /// Whether a resolved type argument names something Core Keeper owns.
        /// </summary>
        /// <remarks>
        /// The suffix test is applied AFTER the name has been resolved through the aliases and
        /// stripped of <c>global::</c>, the verbatim <c>@</c> and its namespace. The pattern this
        /// replaces was applied to the raw text, so <c>using NewThing = Pug.SomethingAuthoring;</c>
        /// followed by <c>Attach&lt;NewThing&gt;(root)</c> matched nothing at all and shipped
        /// silently — which is the hole the remark on that pattern claimed to have closed.
        /// </remarks>
        private static bool IsACoreKeeperSurfaceName(string resolved)
        {
            return resolved.EndsWith("Authoring", StringComparison.Ordinal) ||
                   resolved.EndsWith("AuthoringComponent", StringComparison.Ordinal) ||
                   resolved.EndsWith("CD", StringComparison.Ordinal) ||
                   resolved == "InteractableObject";
        }

        /// <summary>Every name in a type-argument list, one at a time.</summary>
        private static IEnumerable<string> EveryTypeArgumentIn(string typeArguments)
        {
            foreach (string part in typeArguments.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    yield return trimmed;
                }
            }
        }

        /// <summary>
        /// Non-generic <c>AddComponent</c>, which names its component in a way no scan can follow.
        /// </summary>
        private static readonly Regex AddsAComponentByType = new Regex(
            @"\bAddComponent\s*\(\s*(?!\s*\))",
            RegexOptions.Compiled);

        /// <summary>
        /// The generic calls that take an authoring type and do NOT put one on an object.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Anything not in this list and not in <see cref="AddsAComponent"/> is a form nobody has
        /// classified, and the test says so instead of quietly ignoring it — an add helper that
        /// goes unrecognised is exactly how a new authoring surface ships without anybody having
        /// read its query.
        /// </para>
        /// <para>
        /// WHICH MAKES THIS LIST THE CHEAPEST WAY TO TURN THE WHOLE GUARD OFF. One word here and a
        /// brand-new add helper is invisible to all three scans at once: its components are never
        /// questioned, its caller is never unclassified, and the file it lives in stops counting as
        /// one that writes a surface, so it needs no sweep either. Its own remark used to say the
        /// list "came from reading every such call in the tree", and nine of its thirty names had
        /// no such call anywhere —
        /// <c>GetComponentsInChildren</c>, <c>GetComponentInParent</c>, <c>TryGetComponent</c>,
        /// <c>SingleAuthoringComponentConverter</c>, <c>HashSet</c>, <c>IEnumerable</c>,
        /// <c>Dictionary</c>, <c>Func</c> and <c>Action</c> never take a Core Keeper surface in
        /// this tree. They are gone, and
        /// <see cref="EveryNameExcusedFromAddingIsAFormTheTreeReallyUses"/> now keeps the claim
        /// true: a name that stops being used has to come out, so nobody can leave a spare excuse
        /// lying about for a helper that arrives later.
        /// </para>
        /// <para>
        /// The add alternation is deliberately NOT held to the same rule.
        /// <c>AddComponentData</c> and <c>GetOrAddComponent</c> have no call in the tree and are
        /// carried on purpose: an unused name there can only make the guard ask about more, and an
        /// unused name here makes it ask about less.
        /// </para>
        /// </remarks>
        private static readonly HashSet<string> GenericCallsThatDoNotAddAComponent =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "GetComponent", "GetComponents", "GetComponentInChildren",
                "RemoveComponentIfPresent", "TryRemoveComponent", "HasComponent", "List",

                // The ECS side: reading, querying and removing a runtime component. None of these
                // puts one on anything.
                "GetComponentData", "TryGetComponentData", "HasComponentData", "IsComponentEnabled",
                "GetSingleton", "RequireForUpdate", "ReadOnly", "ReadWrite", "Exclude", "RefRO",
                "RemoveComponent", "NativeArray", "ToComponentDataArray", "BlobBuilderArray",
            };

        /// <summary>
        /// Every name excused from adding is a call the tree really makes on a Core Keeper surface.
        /// </summary>
        /// <remarks>
        /// The excuse list is the one place where a single word switches the guard off for a whole
        /// helper, and nothing checked that its names were even used. A name that no longer takes a
        /// Core Keeper surface anywhere is a standing invitation: write a new helper, call it one of
        /// those names, and every scan in this file passes over it. So the list has to shrink when
        /// the tree does.
        /// </remarks>
        [Test]
        public void EveryNameExcusedFromAddingIsAFormTheTreeReallyUses()
        {
            HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);

            foreach (ScannedFile file in ScanTheFramework())
            {
                foreach (Match match in AnyGenericCall.Matches(file.Code))
                {
                    string caller = match.Groups[1].Value;
                    if (!GenericCallsThatDoNotAddAComponent.Contains(caller))
                    {
                        continue;
                    }

                    foreach (string argument in EveryTypeArgumentIn(match.Groups[2].Value))
                    {
                        string component = file.Resolve(argument);

                        // The same two filters the unclassified scan applies, so this list is
                        // measured against exactly the calls that scan would have questioned.
                        if (component.StartsWith("Dimension", StringComparison.Ordinal) ||
                            !IsACoreKeeperSurfaceName(component))
                        {
                            continue;
                        }

                        used.Add(caller);
                        break;
                    }
                }
            }

            List<string> neverUsed = new List<string>();
            foreach (string name in GenericCallsThatDoNotAddAComponent)
            {
                if (!used.Contains(name))
                {
                    neverUsed.Add(name);
                }
            }

            neverUsed.Sort(StringComparer.Ordinal);

            Assert.That(
                neverUsed,
                Is.Empty,
                "GenericCallsThatDoNotAddAComponent excuses a name that nothing in the framework " +
                "passes a Core Keeper component to. An excuse nobody needs is a place for a new " +
                "add helper to hide, because a name on this list is skipped by the component scan, " +
                "by the unclassified-form scan and by the sweep check all at once. Take the name " +
                "out; if the call comes back, the unclassified-form test will ask for it again and " +
                "whoever puts it back will have read what it does:\n  " +
                string.Join("\n  ", neverUsed));
        }

        [Test]
        public void EveryComponentTheGeneratorsAddHasBeenReadAgainstItsSystem()
        {
            Dictionary<string, string> answered = TheAnswerLists();

            List<string> unanswered = new List<string>();
            HashSet<string> alreadySaid = new HashSet<string>(StringComparer.Ordinal);

            foreach (ScannedFile file in ScanTheFramework())
            {
                foreach (Match match in AddsAComponent.Matches(file.Code))
                {
                foreach (string argument in EveryTypeArgumentIn(match.Groups[1].Value))
                {
                    string component = file.Resolve(argument);

                    // The framework's own components answer to the framework's own systems,
                    // whose queries are in this repository and are read where they are written.
                    if (component.StartsWith("Dimension", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    // Not a suffix test any more. A Core Keeper surface is anything that is not on
                    // the written-down list of things that are not one, so InteractableObject and
                    // the runtime CD components can no longer walk past the question.
                    if (NotACoreKeeperSurface.Contains(component))
                    {
                        continue;
                    }

                    if (answered.ContainsKey(component))
                    {
                        continue;
                    }

                    if (alreadySaid.Add(component))
                    {
                        unanswered.Add(
                            component + "  (" + file.Name + ":" + file.LineAt(match.Index) + ")");
                    }
                }
                }

                // A COMPONENT COPIED RATHER THAN NAMED. There are none today, and this refuses
                // the form so the first one cannot arrive in silence.
                foreach (Match copy in CopiesAComponent.Matches(file.Code))
                {
                    string where = file.Name + ":" + file.LineAt(copy.Index);
                    if (alreadySaid.Add(where))
                    {
                        unanswered.Add(
                            "a component copied from another object at " + where +
                            ", which names no component this scan can read");
                    }
                }
            }

            unanswered.Sort(StringComparer.Ordinal);

            Assert.That(
                unanswered,
                Is.Empty,
                "A generator has started putting a NEW Core Keeper component on an object, and " +
                "nobody has said what the system that reads it also needs. Read that system's " +
                "query. If it needs something else beside this component, add a row to " +
                "DimensionQueryCompanions so the sweep supplies it or says in words why it cannot. " +
                "If the query is already satisfied by what we write, add the name to " +
                "ReadAgainstTheirSystemAndNeedNothingBeside. If you read it and wrote the verdict " +
                "into E:\\ck mods\\ck-research\\query-match-census.md, add it to " +
                "ReadAgainstTheirSystemAndTheVerdictIsRecorded. " +
                "NobodyHasReadTheSystemThatConsumesTheseYet is not a place to put a new surface — " +
                "it names what was already left over when this test was written, and " +
                "shipping one unread is exactly how a feature ends up generating cleanly and " +
                "doing nothing:\n  " +
                string.Join("\n  ", unanswered));
        }

        /// <summary>
        /// Runs the production sweep over a probe for every row, and reads the object afterwards.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS TEST USED TO AUDIT ITSELF. It called <c>CloseTheGaps</c> and then
        /// <c>WhatIsStillMissing</c>, and the second of those runs the identical row logic and
        /// PERFORMS EVERY FILL while it looks — its own remark says so. So replacing the body of
        /// <c>CloseTheGaps</c> with nothing left this test green: the listing had done the work the
        /// listing was reporting on, the sentence list came back empty and the only other
        /// assertion iterated zero times. The one guard named for proving the companions work was
        /// the one thing in the file that could not fail when the companions stopped working.
        /// </para>
        /// <para>
        /// It now reads the probe with <c>WhatIsStillMissingWithoutTouchingIt</c>, which only
        /// looks. Every gap it reports is a gap the production sweep really left, and the count of
        /// gaps the sweep closed is asserted to be greater than zero, so an empty sweep fails on
        /// both halves rather than passing on both.
        /// </para>
        /// <para>
        /// AND A ROW THAT SAYS IT FILLS SOMETHING HAS TO HAVE FILLED IT. Four rows carry a fill and
        /// a sentence, and the old check excused any gap that was spoken about — so gutting one of
        /// those four fills only changed the entry from "says nothing" to "says", which was
        /// skipped. Words excuse a row that has nothing but words.
        /// </para>
        /// <para>
        /// "A ROW THAT FILLS" IS PER OBJECT, WHICH IS WHY THIS TEST WAS RED WHEN IT WAS WRITTEN.
        /// The marker used to be stamped from <c>FillIn != null</c> alone, and three of those four
        /// rows had handed a pure READ in as their fill — is this blast big enough, does this
        /// creature belong to somebody — while the fourth, the door, can only be filled on a door
        /// with something to use on it. On the bare two-component probe every one of them answers
        /// no, correctly, and speaks; the marker said they had claimed to fill it and this test
        /// counted four working rows as four breaks. The three reads are no longer fills at all,
        /// and the door declares its precondition in <c>Companion.OnlyWhen</c>, which the listing
        /// asks before it stamps.
        /// </para>
        /// <para>
        /// AND EVERY SENTENCE HAS TO HAVE BEEN SAID. Nothing used to check that: the sentence in
        /// the listing is read off the row, not off anything emitted, so deleting the whole
        /// <c>say</c> block in <c>CloseTheGaps</c> left every test in this file green and took ten
        /// rows whose entire value is a warning down with it. A gap that survives the sweep is
        /// looked up in what the sweep actually said.
        /// </para>
        /// </remarks>
        [Test]
        public void EveryRowThatSaysItCanCloseAGapDoesCloseIt()
        {
            List<string> broken = new List<string>();
            int gapsTheSweepClosed = 0;
            int probesWithAGapBeforeTheSweep = 0;

            foreach (string component in DimensionQueryCompanions.CoveredAuthoringComponents())
            {
                Type type = FindAuthoringType(component);
                if (type == null)
                {
                    broken.Add(component + " is named by a row and is not a type this build has.");
                    continue;
                }

                GameObject probe = new GameObject(component + "Probe");
                try
                {
                    probe.AddComponent<ObjectAuthoring>();
                    probe.AddComponent(type);

                    // READ FIRST, so the count below is what the SWEEP did and not what the
                    // reading did. Nothing between these two lines touches the object.
                    List<string> before =
                        DimensionQueryCompanions.WhatIsStillMissingWithoutTouchingIt(probe);

                    List<string> said = new List<string>();
                    DimensionQueryCompanions.CloseTheGaps(probe, "probe", said.Add);

                    List<string> missing =
                        DimensionQueryCompanions.WhatIsStillMissingWithoutTouchingIt(probe);

                    if (before.Count > 0)
                    {
                        probesWithAGapBeforeTheSweep++;
                    }

                    if (before.Count > missing.Count)
                    {
                        gapsTheSweepClosed += before.Count - missing.Count;
                    }

                    for (int i = 0; i < missing.Count; i++)
                    {
                        // A ROW THAT CLAIMS TO FILL ITS OWN GAP GETS NO EXCUSE. Words are for a
                        // row that has only words.
                        if (missing[i].Contains(DimensionQueryCompanions.TheRowSaysItFillsThisIn))
                        {
                            broken.Add(
                                missing[i] +
                                " — the sweep ran and it is still not there.");
                            continue;
                        }

                        // EVERY remaining gap on the probe, not only the probed component's own.
                        // A fill that adds a component which itself has a row is the bug class one
                        // level down, and dropping those entries meant nothing ever checked that
                        // the sweep settles.
                        //
                        // A gap is allowed to stay open only when THIS row tells the author about
                        // it in words. Asking whether the probe produced any sentence at all let
                        // one row's sentence excuse every other row's silence on the same object —
                        // WayPointAuthoring was the live instance, with two rows and one sentence.
                        // Silence is the defect, not the gap.
                        if (!missing[i].EndsWith(
                                DimensionQueryCompanions.SaysNothingAboutIt,
                                StringComparison.Ordinal))
                        {
                            // AND THE SENTENCE HAS TO HAVE REACHED THE AUTHOR. The listing reads
                            // the words off the row, so a row can carry a perfect sentence that
                            // nothing ever emits — delete the say block in CloseTheGaps and every
                            // other assertion here stays green while ten warnings go silent. The
                            // sweep was given a sink above; this is what it put in it.
                            int wordsStart = missing[i].IndexOf(
                                DimensionQueryCompanions.SaysThis,
                                StringComparison.Ordinal);
                            if (wordsStart < 0)
                            {
                                continue;
                            }

                            string words = missing[i].Substring(
                                wordsStart + DimensionQueryCompanions.SaysThis.Length);
                            if (!said.Contains("'probe' " + words))
                            {
                                broken.Add(
                                    missing[i] +
                                    " — the row has those words and the sweep never said them.");
                            }

                            continue;
                        }

                        broken.Add(missing[i] + " — and nothing was said about it.");
                    }

                    // Every sentence has to name the object it is about. A generation report is a
                    // list of lines from a whole run, and one that does not say which thing it is
                    // talking about cannot be acted on.
                    for (int i = 0; i < said.Count; i++)
                    {
                        if (!said[i].StartsWith("'probe'", StringComparison.Ordinal))
                        {
                            broken.Add(
                                component + " said \"" + said[i] +
                                "\", which does not name the object it is about.");
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(probe);
                }
            }

            Assert.That(
                broken,
                Is.Empty,
                "A companion row claims to supply what a system also needs, and after the sweep ran " +
                "it is still not there — or it cannot be supplied and nothing was said. Either way " +
                "the object generates cleanly and the feature does nothing:\n  " +
                string.Join("\n  ", broken));

            // THE SWEEP HAS TO HAVE DONE SOMETHING. Both assertions above are satisfied by an
            // object with no gaps, and an object the sweep never touched has no gaps only because
            // nothing looked. This one fails the moment CloseTheGaps stops closing anything —
            // which is the state the whole file exists to keep the tree out of, and the state it
            // could not detect while the listing performed the fills itself.
            Assert.That(
                gapsTheSweepClosed,
                Is.GreaterThan(0),
                "CloseTheGaps ran over " + probesWithAGapBeforeTheSweep + " probes that had an " +
                "open gap before it ran, and closed none of them. Every generated creature, " +
                "container, door, crafting station, vehicle, crop and boss would ship with none " +
                "of the companion components and none of the sentences: they would generate " +
                "cleanly and do nothing.");
        }

        /// <summary>
        /// The rows that answer with words alone, and why each of them cannot be filled in.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WITHOUT THIS, TURNING A FILL INTO A WARNING IS A GREEN TWO-LINE EDIT. Every other check
        /// on a row's fill only fires when there IS one, so setting a row's fill to null and giving
        /// it a sentence passes them all — and the sweep's own "it closed something" counter is one
        /// number over every row, so the other thirty-three keep it above zero. The trader's shelves
        /// could stop being built and every generated trader would ship inert with a warning.
        /// </para>
        /// <para>
        /// So the seven are written down with the reason. Adding a row here is meant to be work:
        /// the reason has to be true, and if a gap can be filled from what is already on the object
        /// then filling it is the answer and words are not.
        /// </para>
        /// </remarks>
        private static readonly Dictionary<string, string> AnsweredWithWordsBecauseNothingHereCanFillIt =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {
                    "BedAuthoring",
                    "where the sleeper's head and feet go are offsets only the author knows; a " +
                    "default puts them on the bed's exact centre and they stand straight back up"
                },
                {
                    "WayPointAuthoring",
                    "the game only lights up a travel point that carries its own map pin as a " +
                    "separate little object beside it, and no generator builds that pin"
                },
                {
                    "SpawnTileOnExplosionAuthoring",
                    "only a blast can lay ground where it goes off, and on a world object there " +
                    "is no blast for the author to have sized"
                },
                // "BeamAttackStateAuthoring" was here, saying the beam list had no producer
                // anywhere in the game. That was wrong: BeamAttackStateSystem adds its own beams
                // at :133 and :141 and its query only asks that the buffer exists. Its row now
                // fills the gap in with a converter of the framework's own, so it belongs nowhere
                // near a list of things nothing can supply.
                {
                    "SpawnerAuthoring",
                    "nothing in Core Keeper reads that mark at all"
                },
                {
                    "DontDestroyOnZeroHealthAuthoring",
                    "how much health a thing has is the author's answer, and a number invented " +
                    "here would decide how hard it is to knock down"
                },
                {
                    "TouchAttackAuthoring",
                    "the game only runs a touch attack on something somebody summoned, and " +
                    "nothing here can decide that a creature is a pet"
                },
                {
                    "MinionOrbitAuthoring",
                    "the same: an orbit needs somebody to orbit, and only the author knows whether " +
                    "there is one"
                },
            };

        [Test]
        public void TheRowsThatAnswerWithWordsAloneAreTheOnesWeKnowAbout()
        {
            List<string> wrong = new List<string>();
            HashSet<string> found = new HashSet<string>(StringComparer.Ordinal);

            foreach (string row in DimensionQueryCompanions.RowsThatOnlyTheAuthorCanClose())
            {
                int colon = row.IndexOf(':');
                string component = colon < 0 ? row : row.Substring(0, colon);
                string words = colon < 0 ? string.Empty : row.Substring(colon + 1).Trim();

                found.Add(component);

                if (!AnsweredWithWordsBecauseNothingHereCanFillIt.ContainsKey(component))
                {
                    wrong.Add(
                        component + " answers with words and used to fill its gap in. If that was " +
                        "deliberate, write down here why nothing on the object can supply it.");
                }

                if (words.Length == 0)
                {
                    wrong.Add(component + " has neither a fill nor anything to say.");
                }
            }

            foreach (KeyValuePair<string, string> known in AnsweredWithWordsBecauseNothingHereCanFillIt)
            {
                if (!found.Contains(known.Key))
                {
                    wrong.Add(
                        known.Key + " is listed here as answerable only in words and its row now " +
                        "fills the gap in, or the row is gone. Take it off this list.");
                }
            }

            wrong.Sort(StringComparer.Ordinal);

            Assert.That(
                wrong,
                Is.Empty,
                "The rows that can only warn are no longer the ones written down here. A fill " +
                "quietly turned into a warning is a feature that generates cleanly and does " +
                "nothing, and every other check in this file passes over it:\n  " +
                string.Join("\n  ", wrong));
        }

        [Test]
        public void TheUnreadBacklogNamesOnlyThingsTheGeneratorsStillWrite()
        {
            HashSet<string> stillAdded = ComponentsTheGeneratorsAdd();

            List<string> stale = new List<string>();
            foreach (string name in NobodyHasReadTheSystemThatConsumesTheseYet)
            {
                if (!stillAdded.Contains(name))
                {
                    stale.Add(name);
                }
            }

            stale.Sort(StringComparer.Ordinal);

            Assert.That(
                stale,
                Is.Empty,
                "The backlog of components nobody has read against their system names things the " +
                "generators no longer write. A backlog that keeps names nothing adds any more " +
                "reads as bigger work than it is, and hides the ones that still matter. Take these " +
                "out:\n  " + string.Join("\n  ", stale));
        }

        /// <summary>
        /// Every name that has been answered, and which list answered it.
        /// </summary>
        /// <remarks>
        /// ONE TABLE, NOT FOUR SETS CONSULTED IN TURN. Four sets meant a name could sit in two of
        /// them, and 25 of the 26 companion rows did — in the list documented as "needs nothing
        /// beside them", which is the opposite of what a row says. Deleting a row then left the
        /// build green. Building one table makes the second entry a collision that something can
        /// report, which is what <see cref="NoNameSitsInMoreThanOneOfTheAnswerLists"/> does.
        /// </remarks>
        private static Dictionary<string, string> TheAnswerLists()
        {
            return TheAnswerLists(new List<string>());
        }

        private static Dictionary<string, string> TheAnswerLists(List<string> saidTwice)
        {
            Dictionary<string, string> answered =
                new Dictionary<string, string>(StringComparer.Ordinal);

            FoldIn(
                answered,
                saidTwice,
                DimensionQueryCompanions.CoveredAuthoringComponents(),
                "a companion row");
            FoldIn(
                answered,
                saidTwice,
                ReadAgainstTheirSystemAndNeedNothingBeside,
                "ReadAgainstTheirSystemAndNeedNothingBeside");
            FoldIn(
                answered,
                saidTwice,
                ReadAgainstTheirSystemAndTheVerdictIsRecorded,
                "ReadAgainstTheirSystemAndTheVerdictIsRecorded");
            FoldIn(
                answered,
                saidTwice,
                NobodyHasReadTheSystemThatConsumesTheseYet,
                "NobodyHasReadTheSystemThatConsumesTheseYet");

            return answered;
        }

        private static void FoldIn(
            Dictionary<string, string> answered,
            List<string> saidTwice,
            IEnumerable<string> names,
            string which)
        {
            foreach (string name in names)
            {
                string already;
                if (answered.TryGetValue(name, out already))
                {
                    saidTwice.Add(name + " is answered by " + already + " AND by " + which);
                    continue;
                }

                answered[name] = which;
            }
        }

        [Test]
        public void NoNameSitsInMoreThanOneOfTheAnswerLists()
        {
            List<string> saidTwice = new List<string>();
            TheAnswerLists(saidTwice);
            saidTwice.Sort(StringComparer.Ordinal);

            Assert.That(
                saidTwice,
                Is.Empty,
                "A component is answered in two places at once, and the two say different things. " +
                "A companion row says its system needs something else beside the component; " +
                "ReadAgainstTheirSystemAndNeedNothingBeside says it needs nothing. While both were " +
                "consulted, deleting the row left the build green and the feature silently broken " +
                "— which is the failure this whole file exists to stop. Pick one and delete the " +
                "other:\n  " + string.Join("\n  ", saidTwice));
        }

        [Test]
        public void EveryNameApprovedOrBackloggedIsStillWrittenSomewhere()
        {
            HashSet<string> stillAdded = ComponentsTheGeneratorsAdd();

            List<string> stale = new List<string>();
            foreach (KeyValuePair<string, string> answer in TheAnswerLists())
            {
                if (answer.Value == "a companion row")
                {
                    // A row may name a component the generators do not write yet; the row is the
                    // knowledge, and EveryRowThatSaysItCanCloseAGapDoesCloseIt exercises it
                    // directly rather than through the generators.
                    continue;
                }

                if (!stillAdded.Contains(answer.Key))
                {
                    stale.Add(answer.Key + "  (" + answer.Value + ")");
                }
            }

            stale.Sort(StringComparer.Ordinal);

            Assert.That(
                stale,
                Is.Empty,
                "A list here names a component nothing in the framework writes any more. On the " +
                "backlog that reads as more work than there is; on either of the two read lists it " +
                "is worse, because it is a standing approval for a surface nobody is writing and " +
                "the next person to add it gets no question at all. Take these out:\n  " +
                string.Join("\n  ", stale));
        }

        /// <summary>
        /// The census file the "verdict is recorded" list points at.
        /// </summary>
        /// <remarks>
        /// It is outside the Unity project, which is why nothing used to read it. That is exactly
        /// what made the list's promise unbacked: moving a name into it from the unread backlog
        /// left every test green, because the only thing that ever checked the verdict had been
        /// written was the person writing it.
        /// </remarks>
        private const string TheCensus = @"E:\ck mods\ck-research\query-match-census.md";

        [Test]
        public void EveryNameSaidToHaveAVerdictHasOneInTheCensus()
        {
            if (!File.Exists(TheCensus))
            {
                Assert.Ignore(
                    "The census is not on this machine (" + TheCensus + "), so this run did not " +
                    "check that ReadAgainstTheirSystemAndTheVerdictIsRecorded's names have written " +
                    "verdicts. Reported as skipped rather than passed: a green here would say the " +
                    "promise was checked when nothing opened the file. The census is research " +
                    "material outside the Unity project and is not copied in, because a 400 KB " +
                    "markdown under Assets ships inside every mod built with the framework.");
            }

            string census = File.ReadAllText(TheCensus);

            List<string> unrecorded = new List<string>();
            foreach (string name in ReadAgainstTheirSystemAndTheVerdictIsRecorded)
            {
                if (census.IndexOf(name, StringComparison.Ordinal) < 0)
                {
                    unrecorded.Add(name);
                }
            }

            unrecorded.Sort(StringComparer.Ordinal);

            Assert.That(
                unrecorded,
                Is.Empty,
                "These names are in ReadAgainstTheirSystemAndTheVerdictIsRecorded and the census " +
                "does not mention them, so the verdict that list promises was never written. " +
                "Either write it, or move the name back into " +
                "NobodyHasReadTheSystemThatConsumesTheseYet where it belongs:\n  " +
                string.Join("\n  ", unrecorded));
        }

        [Test]
        public void TheScanReadsRealFilesAndFindsRealAdds()
        {
            List<ScannedFile> scanned = ScanTheFramework();

            Assert.That(
                scanned.Count,
                Is.GreaterThan(50),
                "The scan found almost no source files, so every other test in this file passed " +
                "over nothing. GeneratorSources walks Application.dataPath/ExpandNullforge and " +
                "yields nothing at all when that folder is renamed, moved into a package, or the " +
                "tests are run from somewhere else — and nothing used to notice.");

            Assert.That(
                ComponentsTheGeneratorsAdd().Count,
                Is.GreaterThan(200),
                "The scan read files and matched almost nothing in them. The add helpers have been " +
                "renamed out from under AddsAComponent, so the guard is reporting success over " +
                "code it can no longer read.");
        }

        /// <summary>
        /// The files that put a Core Keeper surface on something and never run the sweep, each with
        /// the reason.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NOTHING USED TO CHECK THAT THE SWEEP RAN AT ALL. Every row in the companion table only
        /// does anything if something calls <c>CloseTheGaps</c> over the finished object, and
        /// seventeen files write Core Keeper surfaces without any call reaching them. Two of the
        /// generators were admitted in the last report; the tileset block, ore and farming path and
        /// the runtime bootstrap were not mentioned anywhere. Writing the list down turns "nobody
        /// checked" into "somebody said why", and the test below fails both ways: a new unswept
        /// file, and a name here that has started sweeping or stopped writing surfaces.
        /// </para>
        /// <para>
        /// A file counts as sweeping if it calls <c>CloseTheGaps</c> or one of the three
        /// <c>Finish…</c> helpers that ends in one.
        /// </para>
        /// </remarks>
        private static readonly Dictionary<string, string> WritesASurfaceAndDoesNotSweep =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                {
                    "DimensionObjectSpine.cs",
                    "a shared helper, not a generator. The reason it is safe is narrower than " +
                    "this entry used to claim: six files call into the spine and never sweep — " +
                    "the two blast generators, the projectile generator, the block authoring, the " +
                    "runtime loader and the world-rules generator — so 'whoever owns the object " +
                    "sweeps afterwards' is not true of every caller. It holds because every spine " +
                    "method that writes a component a companion row names — ApplyExplosive, " +
                    "ApplyChainReaction, ApplyObjectRules, and the eight the creature generator " +
                    "was taught to call (ApplyContinuousAttack, ApplyBeamAndAmbience, " +
                    "ApplyHidingAndHatching, ApplyManaAndAura, ApplyFinalTouches, " +
                    "ApplySpawnerAndOrb, ApplyTrader, ApplyCreatureLastStand) — is reached only " +
                    "from the item, world-object and creature generators, and all three sweep. " +
                    "That is a fact about today's call graph and nothing checks it"
                },
                {
                    "DimensionInteractionVisualUtility.cs",
                    "a shared helper for the picture and the usable part, called from generators " +
                    "that sweep afterwards"
                },
                {
                    "DimensionDropEmitter.cs",
                    "a shared helper that writes the loot answer, called from generators that " +
                    "sweep afterwards"
                },
                {
                    "DimensionExplosionGenerator.cs",
                    "a blast is not placed in the world and two rows are worded for something " +
                    "that is, so the sweep would tell the author the wrong thing. Whether those " +
                    "rows should be split has not been decided"
                },
                {
                    "DimensionExplosiveBlast.cs",
                    "the same blast surfaces as the blast generator, for the same reason"
                },
                {
                    "DimensionProjectileGenerator.cs",
                    "a shot is not placed in the world, same reason as the blast"
                },
                {
                    "DimensionTilesetBlockAuthoring.cs",
                    "nobody has decided whether a block belongs in the sweep. Slice D read its " +
                    "surfaces and none of them has a row today, so running it would change " +
                    "nothing yet — but nothing checks that after the next row is added"
                },
                {
                    "DimensionTilesetOreAuthoring.cs",
                    "same as the block: not yet decided, no row touches its surfaces today"
                },
                {
                    "DimensionTilesetFarmingAuthoring.cs",
                    "same as the block: not yet decided, no row touches its surfaces today"
                },
                {
                    "DimensionRuntimeConsumerBootstrapUtility.cs",
                    "it edits prefabs the generators have already built and swept, and the portal " +
                    "prefabs it builds itself have not been read against the rows"
                },
                {
                    "NullforgeDimensionService.RuntimeLoadingInternals.cs",
                    "it puts components on entities while the game runs; the sweep works on a " +
                    "GameObject before conversion and has nothing to act on there"
                },
                {
                    "DimensionDungeonAssembler.cs",
                    "components on a running entity, same as the runtime loader"
                },
                {
                    "DimensionItemPortalSpawnSystem.cs",
                    "components on a running entity, same as the runtime loader"
                },
                {
                    "DimensionPortalAuthoringConverter.cs",
                    "a converter, which runs after conversion has begun and has no GameObject"
                },
                {
                    "DimensionBeamBufferAuthoring.cs",
                    "a converter, same as the portal one: it runs after conversion has begun and " +
                    "has no GameObject to sweep. What it adds is the one line Core Keeper's own " +
                    "BeamAttackStateConverter is missing — the beam buffer and the attack cooldown " +
                    "that BeamAttackStateSystem's query names — and the object it rides on is a " +
                    "creature, which the creature generator sweeps"
                },

                // THE VIEW PATH, which was invisible to every test in this file until the four
                // Core Keeper types it writes were taken off the list of things that are not a
                // Core Keeper surface. Named here rather than swept, with the reason.
                {
                    "DimensionCreatureViewBuilder.cs",
                    "it builds the picture — the body sprite, the shadow and the damage flash — " +
                    "on child objects under a creature the creature generator sweeps afterwards"
                },
                {
                    "DimensionPlantViewBuilder.cs",
                    "the same picture-building for a crop, under a prefab the plant generator " +
                    "sweeps afterwards"
                },
                {
                    "DimensionFloatingTextUtility.cs",
                    "a shared helper for the floating name and number, built on child objects of " +
                    "something a generator sweeps"
                },
            };

        [Test]
        public void EveryFileThatWritesASurfaceEitherSweepsOrSaysWhyNot()
        {
            List<string> unexplained = new List<string>();
            List<string> stale = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> firstWithThatName =
                new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (ScannedFile file in ScanTheFramework())
            {
                // THE EXCUSES ARE KEYED BY FILE NAME, so two files with the same name share one.
                // A second DimensionObjectSpine.cs in a subfolder that wrote surfaces and never
                // swept would inherit the first one's excuse AND be marked as having been seen, so
                // the staleness half stayed quiet too. There are none today; this is what keeps it
                // that way, because keying on the whole path instead would mean writing seventeen
                // paths down and moving a file would then be the silent break.
                string alreadyHere;
                if (firstWithThatName.TryGetValue(file.Name, out alreadyHere))
                {
                    stale.Add(
                        file.Name + " is the name of two files in the framework (" + alreadyHere +
                        " and " + file.Where + "), and the excuses in " +
                        "WritesASurfaceAndDoesNotSweep are keyed by name, so one would silently " +
                        "answer for the other");
                }
                else
                {
                    firstWithThatName.Add(file.Name, file.Where);
                }

                bool writesASurface = false;
                foreach (Match match in AddsAComponent.Matches(file.Code))
                {
                    foreach (string argument in EveryTypeArgumentIn(match.Groups[1].Value))
                    {
                        string component = file.Resolve(argument);
                        if (!component.StartsWith("Dimension", StringComparison.Ordinal) &&
                            !NotACoreKeeperSurface.Contains(component))
                        {
                            writesASurface = true;
                            break;
                        }
                    }

                    if (writesASurface)
                    {
                        break;
                    }
                }

                if (!writesASurface)
                {
                    continue;
                }

                // READ WITH THE STRING LITERALS BLANKED TOO. Comments were already blanked and
                // strings were not, so a warning that mentioned the sweep by name — or any other
                // sentence with the word in it — satisfied this check without a call existing.
                //
                // AND WITH THE PREPROCESSOR BLOCKS BLANKED. A call inside "#if NEVER_DEFINED" is
                // text the compiler never sees, and the check knew nothing about that, so a sweep
                // that had been switched off at the top of the file still read as a sweep.
                bool sweeps = RunsTheSweep.IsMatch(
                    WithConditionalBlocksBlanked(file.CodeWithoutText));
                bool excused = WritesASurfaceAndDoesNotSweep.ContainsKey(file.Name);

                if (!sweeps && !excused)
                {
                    unexplained.Add(file.Name);
                }

                if (sweeps && excused)
                {
                    stale.Add(file.Name + " is listed as not sweeping and does sweep");
                }

                if (excused)
                {
                    seen.Add(file.Name);
                }
            }

            foreach (KeyValuePair<string, string> excused in WritesASurfaceAndDoesNotSweep)
            {
                if (!seen.Contains(excused.Key))
                {
                    stale.Add(excused.Key + " is listed and no longer writes a Core Keeper surface");
                }
            }

            unexplained.Sort(StringComparer.Ordinal);
            stale.Sort(StringComparer.Ordinal);

            Assert.That(
                unexplained,
                Is.Empty,
                "A file puts Core Keeper components on something and nothing ever runs the sweep " +
                "over what it built, so every companion row is inert for everything it makes. " +
                "Either call CloseTheGaps once the object is finished, or add the file to " +
                "WritesASurfaceAndDoesNotSweep with the reason:\n  " +
                string.Join("\n  ", unexplained));

            Assert.That(
                stale,
                Is.Empty,
                "WritesASurfaceAndDoesNotSweep no longer describes the tree:\n  " +
                string.Join("\n  ", stale));
        }

        /// <summary>The sweep itself, and the three helpers that end in it.</summary>
        private static readonly Regex RunsTheSweep = new Regex(
            @"\b(?:CloseTheGaps|FinishAWorldObject|FinishACreature|FinishACritter)\s*\(",
            RegexOptions.Compiled);

        [Test]
        public void EveryRowNamesSomethingRealAsWhatIsAlsoNeeded()
        {
            HashSet<string> valueGaps = new HashSet<string>(
                DimensionQueryCompanions.GapsThatAreAValueRatherThanAComponent,
                StringComparer.Ordinal);

            List<string> unreal = new List<string>();
            foreach (string needed in DimensionQueryCompanions.EveryThingARowSaysIsAlsoNeeded())
            {
                if (valueGaps.Contains(needed) || FindAuthoringType(needed) != null)
                {
                    continue;
                }

                unreal.Add(needed);
            }

            unreal.Sort(StringComparer.Ordinal);

            Assert.That(
                unreal,
                Is.Empty,
                "A companion row says the system also needs something that is neither a component " +
                "this build has nor one of the named value gaps. A misspelt component name reads " +
                "as a gap nothing can ever close, and gets reported at every generate forever:\n  " +
                string.Join("\n  ", unreal));
        }

        /// <summary>
        /// Fails on a way of adding a component that this test does not recognise.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THE HOLE THIS CLOSES. The scan above knows a fixed list of add helpers by name. Two live
        /// ones were missing from it — <c>ApplyComponent&lt;T&gt;</c> in the item generator and
        /// <c>Set&lt;T&gt;</c> in the block generator — so twenty-two components the generators
        /// really write were invisible, were never in any of the four lists, and the build stayed
        /// green. A helper the scan does not know about is not a small gap: it is a whole generator
        /// whose authoring surfaces nobody has to answer for.
        /// </para>
        /// <para>
        /// So every generic call anywhere in the framework whose type argument is a Core Keeper
        /// authoring component must be either an add helper this test knows, or a call listed as
        /// not adding one. Anything else fails here, by name and by line, and the person who wrote
        /// it says which it is. That is the difference between a guard that can be walked past and
        /// one that cannot.
        /// </para>
        /// </remarks>
        [Test]
        public void EveryGenericCallOnAnAuthoringTypeIsAKnownForm()
        {
            List<string> unclassified = new List<string>();
            HashSet<string> alreadySaid = new HashSet<string>(StringComparer.Ordinal);

            foreach (ScannedFile file in ScanTheFramework())
            {
                foreach (Match match in AnyGenericCall.Matches(file.Code))
                {
                    string caller = match.Groups[1].Value;

                    foreach (string argument in EveryTypeArgumentIn(match.Groups[2].Value))
                    {
                        // RESOLVED FIRST, THEN JUDGED. The pattern this replaces asked whether the
                        // RAW text ended in Authoring or CD, so an alias that did not — the one
                        // form the alias resolver was widened for — never reached the resolver at
                        // all.
                        string component = file.Resolve(argument);

                        if (component.StartsWith("Dimension", StringComparison.Ordinal) ||
                            !IsACoreKeeperSurfaceName(component))
                        {
                            continue;
                        }

                        if (GenericCallsThatDoNotAddAComponent.Contains(caller) ||
                            AddsAComponent.IsMatch(caller + "<X>"))
                        {
                            continue;
                        }

                        if (alreadySaid.Add(caller))
                        {
                            unclassified.Add(
                                caller + "<" + component + ">  (" + file.Name + ":" +
                                file.LineAt(match.Index) + ")");
                        }
                    }
                }

                // AddComponent with a Type instead of a type argument names its component in a
                // way no scan can follow, so it is refused outright rather than missed.
                foreach (Match match in AddsAComponentByType.Matches(file.Code))
                {
                    string where = file.Name + ":" + file.LineAt(match.Index);
                    if (alreadySaid.Add(where))
                    {
                        unclassified.Add(
                            "AddComponent(Type) at " + where +
                            ", which names no component this scan can read");
                    }
                }
            }

            unclassified.Sort(StringComparer.Ordinal);

            Assert.That(
                unclassified,
                Is.Empty,
                "Something in the framework passes a Core Keeper authoring component to a generic " +
                "call this test has never been told about. If it puts the component ON an object, " +
                "add its name to AddsAComponent — otherwise every component it writes is invisible " +
                "to the check above and ships without anybody reading the system that consumes it. " +
                "If it only reads or removes, add its name to " +
                "GenericCallsThatDoNotAddAComponent. If it adds a component named by a Type rather " +
                "than a type argument, use the generic form instead, because nothing can read the " +
                "other one:\n  " + string.Join("\n  ", unclassified));
        }

        /// <summary>Every Core Keeper surface the framework still writes.</summary>
        /// <remarks>
        /// <para>
        /// "WRITES" IS A TEXT MATCH AND IT CANNOT TELL AN ADD FROM A REMOVAL. <c>Toggle&lt;T&gt;</c>
        /// is in the add alternation whichever way its flag is set, so four components the
        /// framework only ever STRIPS — <c>AchievementTrackerAuthoring</c>,
        /// <c>ClientBiomeSamplesAuthoring</c>, <c>ClientSubMapAuthoring</c> and
        /// <c>WaterSpreaderAuthoring</c>, all four refused on purpose because they are world
        /// singletons or delete the object — are counted as components the generators write, and
        /// they sit in an approval list on that footing. The cost is one-sided: a surface demoted
        /// to a removal keeps its answer alive and the staleness check will not notice, which
        /// leaves a stale approval rather than an unanswered component. Reading the flag would
        /// mean parsing an argument, and a scan that reads arguments wrongly loses adds, which is
        /// the expensive direction. Written down rather than guessed at.
        /// </para>
        /// <para>
        /// It also cannot see a component that arrives on a prefab rather than through a call:
        /// <c>Object.Instantiate</c> of a template, and the fifteen generators that regenerate in
        /// place through <c>LoadPrefabContents</c>, both bring whatever the previous prefab had.
        /// </para>
        /// </remarks>
        private static HashSet<string> ComponentsTheGeneratorsAdd()
        {
            HashSet<string> added = new HashSet<string>(StringComparer.Ordinal);

            foreach (ScannedFile file in ScanTheFramework())
            {
                foreach (Match match in AddsAComponent.Matches(file.Code))
                {
                    foreach (string argument in EveryTypeArgumentIn(match.Groups[1].Value))
                    {
                        added.Add(file.Resolve(argument));
                    }
                }
            }

            return added;
        }

        /// <summary>
        /// One source file with its prose blanked out and its using-aliases read.
        /// </summary>
        /// <remarks>
        /// <para>
        /// WHY NOT LINE BY LINE. Every scan here used to be <c>ReadAllLines</c> plus a per-line
        /// regex, and a generic call split across two lines matched none of them —
        /// <c>EnsureComponent&lt;</c> on one line and the type on the next was invisible to all
        /// three. The tree already wraps generic calls at the margin, so one long type name on an
        /// add would have taken that component out of the guard permanently, and out of the backlog
        /// too, because the staleness check reads through the same regex.
        /// </para>
        /// <para>
        /// Prose is blanked rather than dropped so that positions still line up with the file, and
        /// so a commented-out add still cannot keep a dead name alive.
        /// </para>
        /// </remarks>
        private sealed class ScannedFile
        {
            public string Name;

            /// <summary>Where it sits, so two files with one name can be told apart.</summary>
            public string Where;

            public string Code;

            /// <summary>
            /// The same again with the string literals blanked as well as the comments.
            /// </summary>
            /// <remarks>
            /// Only the sweep check reads this. Blanking strings would hide a component name held
            /// in a literal from the add scans, and the tree does hold type names in strings, so
            /// the two readings are kept apart rather than merged.
            /// </remarks>
            public string CodeWithoutText;

            public Dictionary<string, string> Aliases;

            /// <summary>The 1-based line the character at that position is on.</summary>
            public int LineAt(int index)
            {
                int line = 1;
                for (int i = 0; i < index && i < Code.Length; i++)
                {
                    if (Code[i] == '\n')
                    {
                        line++;
                    }
                }

                return line;
            }

            /// <summary>
            /// The component a type argument names, following a using-alias if it is one.
            /// </summary>
            /// <remarks>
            /// <c>using Facing = Pug.X.SomeAuthoring;</c> and then <c>Ensure&lt;Facing&gt;</c> used
            /// to yield "Facing", which failed the old suffix test and was dropped in silence.
            /// The tree aliases a type in eight files, and all eight are <c>Object</c> aliased to
            /// dodge the <c>UnityEngine.Object</c> clash — so this is not yet precedent for
            /// aliasing a Core Keeper type, only proof that the next name clash will produce one.
            /// Nothing decides whether a name is a surface until it has been through here.
            /// </remarks>
            public string Resolve(string typeName)
            {
                string last = LastPartOf(typeName);
                string aliased;
                return Aliases.TryGetValue(last, out aliased) ? aliased : last;
            }
        }

        /// <summary>Every framework source file, read once, prose blanked, aliases resolved.</summary>
        private static List<ScannedFile> ScanTheFramework()
        {
            List<ScannedFile> scanned = new List<ScannedFile>();

            foreach (string path in GeneratorSources())
            {
                ScannedFile file = new ScannedFile();
                file.Name = Path.GetFileName(path);
                file.Where = path.Replace('\\', '/');
                string source = File.ReadAllText(path);
                file.Code = CodeWithoutProse(source);
                file.CodeWithoutText = CodeWithoutProse(source, true);
                file.Aliases = AliasesIn(file.Code);
                scanned.Add(file);
            }

            return scanned;
        }

        /// <summary>
        /// The same text with every <c>#if</c> block turned into blank lines.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The sweep check is a text match, and text inside a preprocessor block the compiler never
        /// takes is not a call. A generator whose only <c>CloseTheGaps</c> sat inside
        /// <c>#if NEVER_DEFINED</c> read as sweeping, and every companion row was inert for
        /// everything it built.
        /// </para>
        /// <para>
        /// It blanks the whole block whichever way the condition would go, because nothing here
        /// knows which symbols a build defines. That is the safe direction: it can only take a
        /// sweep away, which asks a person to look, and never invent one. The four framework files
        /// with a conditional block in them today write no Core Keeper surface, so none of them
        /// reaches this.
        /// </para>
        /// </remarks>
        private static string WithConditionalBlocksBlanked(string source)
        {
            string[] lines = source.Split('\n');
            int depth = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                bool opens = trimmed.StartsWith("#if", StringComparison.Ordinal);
                bool closes = trimmed.StartsWith("#endif", StringComparison.Ordinal);

                if (closes && depth > 0)
                {
                    depth--;
                }

                if (depth > 0 || opens || closes)
                {
                    lines[i] = string.Empty;
                }

                if (opens)
                {
                    depth++;
                }
            }

            return string.Join("\n", lines);
        }

        /// <summary>
        /// The same text with every comment turned into spaces, newlines kept.
        /// </summary>
        private static string CodeWithoutProse(string source)
        {
            return CodeWithoutProse(source, false);
        }

        /// <summary>
        /// The same, optionally blanking the string literals as well.
        /// </summary>
        /// <remarks>
        /// The sweep check is a text match for a call, and it was reading a file whose comments
        /// were blanked and whose strings were not — so a warning sentence that mentioned the
        /// sweep by name satisfied it just as well as a call did.
        /// </remarks>
        private static string CodeWithoutProse(string source, bool alsoBlankText)
        {
            char[] code = source.ToCharArray();
            bool inLineComment = false;
            bool inBlockComment = false;
            bool inString = false;
            bool inChar = false;

            for (int i = 0; i < code.Length; i++)
            {
                char here = code[i];
                char next = i + 1 < code.Length ? code[i + 1] : '\0';

                if (inLineComment)
                {
                    if (here == '\n')
                    {
                        inLineComment = false;
                    }
                    else
                    {
                        code[i] = ' ';
                    }

                    continue;
                }

                if (inBlockComment)
                {
                    if (here == '*' && next == '/')
                    {
                        code[i] = ' ';
                        code[i + 1] = ' ';
                        i++;
                        inBlockComment = false;
                    }
                    else if (here != '\n')
                    {
                        code[i] = ' ';
                    }

                    continue;
                }

                if (inString)
                {
                    if (here == '\\')
                    {
                        if (alsoBlankText)
                        {
                            code[i] = ' ';
                            if (i + 1 < code.Length && code[i + 1] != '\n')
                            {
                                code[i + 1] = ' ';
                            }
                        }

                        i++;
                    }
                    else if (here == '"')
                    {
                        inString = false;
                        if (alsoBlankText)
                        {
                            code[i] = ' ';
                        }
                    }
                    else if (alsoBlankText && here != '\n')
                    {
                        code[i] = ' ';
                    }

                    continue;
                }

                if (inChar)
                {
                    if (here == '\\')
                    {
                        i++;
                    }
                    else if (here == '\'')
                    {
                        inChar = false;
                    }

                    continue;
                }

                if (here == '"')
                {
                    inString = true;
                    if (alsoBlankText)
                    {
                        code[i] = ' ';
                    }
                }
                else if (here == '\'')
                {
                    inChar = true;
                }
                else if (here == '/' && next == '/')
                {
                    code[i] = ' ';
                    inLineComment = true;
                }
                else if (here == '/' && next == '*')
                {
                    code[i] = ' ';
                    code[i + 1] = ' ';
                    i++;
                    inBlockComment = true;
                }
            }

            return new string(code);
        }

        /// <summary>Every <c>using Alias = Some.Type;</c> in the file, by alias.</summary>
        private static Dictionary<string, string> AliasesIn(string code)
        {
            Dictionary<string, string> aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in UsingAlias.Matches(code))
            {
                aliases[match.Groups[1].Value] = LastPartOf(match.Groups[2].Value);
            }

            return aliases;
        }

        private static readonly Regex UsingAlias = new Regex(
            @"^\s*using\s+(\w+)\s*=\s*([\w\.]+)\s*;",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>
        /// The one rule for the six places the game looks an object up by its look.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Six systems call <c>PugDatabase.GetPrimaryPrefabEntity</c> with a look and then set or
        /// remove the object's collider from what came back. The lookup answers nothing for a look
        /// nobody registered, so on a framework object four of them DELETED the collider — a locked
        /// chest became unhittable the moment the key went in — and two indexed a lookup with a null
        /// entity inside a Burst job. Saying the object's look is not part of its identity turns all
        /// six into a set of the collider it already has.
        /// </para>
        /// <para>
        /// AND THE TWO EXCLUSIONS ARE PINNED HERE, because they are the reason this is not written
        /// by the sweep every generator reaches. An item's answer belongs to its author, and a crop
        /// is registered at several looks, so <c>CloseTheGaps</c> on its own must leave the field
        /// alone. If that ever changes, rare crops stop being findable and nothing else says so.
        /// </para>
        /// </remarks>
        [Test]
        public void FinishingAPlacedThingSaysItsLookIsNotItsIdentity()
        {
            GameObject finished = new GameObject("LookProbe");
            GameObject swept = new GameObject("SweptOnlyProbe");
            try
            {
                ObjectAuthoring identity = finished.AddComponent<ObjectAuthoring>();
                Assert.That(
                    identity.variationIsDynamic,
                    Is.False,
                    "The probe was supposed to start with the field unset.");

                DimensionQueryCompanions.FinishAWorldObject(finished, "probe", null);

                Assert.That(
                    identity.variationIsDynamic,
                    Is.True,
                    "Finishing a placed object has to say its look is not part of which object it " +
                    "is, or six of the game's own systems look it up under its new look, find " +
                    "nothing, and four of them take its collider away for good.");

                ObjectAuthoring sweptIdentity = swept.AddComponent<ObjectAuthoring>();
                DimensionQueryCompanions.CloseTheGaps(swept, "probe", null);

                Assert.That(
                    sweptIdentity.variationIsDynamic,
                    Is.False,
                    "The sweep on its own must NOT write that field. The item generator writes it " +
                    "from the author's own answer, and the plant generator registers a crop at " +
                    "several looks — marking the first one dynamic would hand every lookup the " +
                    "plain crop and rare crops would quietly stop existing.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(finished);
                UnityEngine.Object.DestroyImmediate(swept);
            }
        }

        /// <summary>
        /// A bed comes out as the two slabs the game's own bed has, not one box over both tiles.
        /// </summary>
        /// <remarks>
        /// <c>BedEntity</c> — the only bed in Core Keeper — carries box (1, 1, 0.2) at
        /// (0, 0.5, -0.3) and box (1, 1, 0.7) at (0, 0.5, 1.15) over a footprint of 1 by 2. The
        /// gap between them is why a player can stand in the middle of a bed. The census row that
        /// said "2 of 2 agree" had compared the layers and the collision response and nothing else,
        /// and the code it was justifying wrote one solid box across the whole footprint.
        /// </remarks>
        [Test]
        public void ABedIsTwoSlabsWithAGapBetweenThem()
        {
            GameObject probe = new GameObject("BedProbe");
            try
            {
                probe.AddComponent<ObjectAuthoring>();
                PlaceableObjectAuthoring placeable = probe.AddComponent<PlaceableObjectAuthoring>();
                placeable.prefabTileSize = new Vector2Int(1, 2);
                probe.AddComponent<BedAuthoring>();

                DimensionQueryCompanions.FinishAWorldObject(probe, "probe", null);

                List<Component> shapes = ShapesOn(probe);
                Assert.That(
                    shapes.Count,
                    Is.EqualTo(2),
                    "A bed has a headboard and a footboard. One box over the whole footprint is a " +
                    "two-tile wall where the game's bed is two thin ends.");

                AssertShape(shapes[0], new Vector3(1f, 1f, 0.2f), new Vector3(0f, 0.5f, -0.3f));
                AssertShape(shapes[1], new Vector3(1f, 1f, 0.7f), new Vector3(0f, 0.5f, 1.15f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// Turning a bed into an ordinary prop leaves it with one shape, not the bed's two.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The generators regenerate onto the prefab they made last time, and the helper that
        /// writes the shapes only ever adds one when the slot is empty. While every placed sort
        /// wrote exactly one shape that could not go wrong; a bed writes two, so a bed turned into
        /// anything else kept its footboard — a solid box a tile away from the object, blocking a
        /// tile, drawn by nothing, with no control that clears it.
        /// </para>
        /// <para>
        /// The second generate here is the same object with the bed answer taken off, which is
        /// exactly what a modder does when they change their mind about what they are building.
        /// </para>
        /// </remarks>
        [Test]
        public void ChangingWhatSomethingIsTakesItsOldShapeOff()
        {
            GameObject probe = new GameObject("WasABedProbe");
            try
            {
                probe.AddComponent<ObjectAuthoring>();
                PlaceableObjectAuthoring placeable = probe.AddComponent<PlaceableObjectAuthoring>();
                placeable.prefabTileSize = new Vector2Int(1, 2);
                BedAuthoring bed = probe.AddComponent<BedAuthoring>();

                DimensionQueryCompanions.FinishAWorldObject(probe, "probe", null);
                Assert.That(ShapesOn(probe).Count, Is.EqualTo(2), "a bed is two slabs");

                UnityEngine.Object.DestroyImmediate(bed);
                DimensionQueryCompanions.FinishAWorldObject(probe, "probe", null);

                List<Component> shapes = ShapesOn(probe);
                Assert.That(
                    shapes.Count,
                    Is.EqualTo(1),
                    "An ordinary prop is one box over its tiles. The second shape is the bed's " +
                    "footboard, left over from when this object was a bed: it blocks a tile a " +
                    "tile away from the object and nothing on the object draws anything there.");

                AssertShape(shapes[0], new Vector3(1f, 1f, 2f), new Vector3(0f, 0.5f, 0.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        /// <summary>
        /// A boat gets the boat's shape, not the minecart's.
        /// </summary>
        /// <remarks>
        /// The 18 vanilla vehicles are three shapes: 14 karts at (1, 1, 0.75) centred
        /// (0, 0.5, 0.125), two boats at (0.01, 1, 0.01) at the same centre, and two minecarts at
        /// the footprint box. The row that said "18 of 18 agree" had compared the layers and the
        /// response — those really are 18 of 18 — and the shape it wrote was the minecart's, which
        /// is 2 of 18. A boat given the whole tile refuses every placement on that tile and
        /// swallows melee swings across it, because Category06 is inside the refusal mask.
        /// </remarks>
        [Test]
        public void ABoatGetsTheBoatsShapeAndAKartGetsTheKarts()
        {
            GameObject boat = new GameObject("BoatProbe");
            GameObject kart = new GameObject("KartProbe");
            try
            {
                boat.AddComponent<ObjectAuthoring>();
                boat.AddComponent<PlaceableObjectAuthoring>().prefabTileSize = Vector2Int.one;
                boat.AddComponent<BoatAuthoring>();
                DimensionQueryCompanions.FinishAWorldObject(boat, "probe", null);
                AssertShape(
                    ShapesOn(boat)[0],
                    new Vector3(0.01f, 1f, 0.01f),
                    new Vector3(0f, 0.5f, 0.125f));

                kart.AddComponent<ObjectAuthoring>();
                kart.AddComponent<PlaceableObjectAuthoring>().prefabTileSize = Vector2Int.one;
                kart.AddComponent<VehicleAuthoring>();
                DimensionQueryCompanions.FinishAWorldObject(kart, "probe", null);
                AssertShape(
                    ShapesOn(kart)[0],
                    new Vector3(1f, 1f, 0.75f),
                    new Vector3(0f, 0.5f, 0.125f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(boat);
                UnityEngine.Object.DestroyImmediate(kart);
            }
        }

        /// <summary>
        /// The shapes on an object, in the order they were added.
        /// </summary>
        /// <remarks>
        /// By name, because this assembly does not reference the physics package and one geometry
        /// type is not worth widening the assembly graph for — the same reason the generator
        /// writes these fields through <c>SerializedObject</c>.
        /// </remarks>
        private static List<Component> ShapesOn(GameObject root)
        {
            List<Component> shapes = new List<Component>();
            foreach (Component component in root.GetComponents<Component>())
            {
                if (component != null &&
                    component.GetType().Name == "PhysicsShapeAuthoring")
                {
                    shapes.Add(component);
                }
            }

            return shapes;
        }

        private static void AssertShape(Component shape, Vector3 size, Vector3 centre)
        {
            UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(shape);
            AssertVector(serialized, "m_PrimitiveSize", size);
            AssertVector(serialized, "m_PrimitiveCenter", centre);
        }

        private static void AssertVector(
            UnityEditor.SerializedObject serialized,
            string path,
            Vector3 expected)
        {
            UnityEditor.SerializedProperty property = serialized.FindProperty(path);
            Assert.That(
                property,
                Is.Not.Null,
                "The physics package no longer serializes " + path + " under that name, so the " +
                "generator has been writing into nothing.");

            Assert.That(
                property.vector3Value.x, Is.EqualTo(expected.x).Within(0.0005f), path + ".x");
            Assert.That(
                property.vector3Value.y, Is.EqualTo(expected.y).Within(0.0005f), path + ".y");
            Assert.That(
                property.vector3Value.z, Is.EqualTo(expected.z).Within(0.0005f), path + ".z");
        }

        [Test]
        public void SeeingNearbyThingsKeepsWhatAnEarlierPassAsked()
        {
            GameObject probe = new GameObject("TrackerProbe");
            try
            {
                // A shove authored at six tiles, watching two layers.
                DimensionQueryCompanions.SeesNearbyThings(probe, 6f, 4u, true);

                // Then the summoning circle's pass, which used to write its own numbers outright.
                DimensionQueryCompanions.SeesNearbyThings(
                    probe,
                    DimensionQueryCompanions.VanillaNoticeRadius,
                    DimensionQueryCompanions.VanillaNoticeLayers,
                    false);

                Assert.That(
                    DimensionQueryCompanions.HowFarItSees(probe),
                    Is.EqualTo(6f),
                    "The later pass narrowed how far the object can see. Everything on the object " +
                    "that watches its surroundings shares one radius, so the widest ask has to win " +
                    "or the other feature silently stops reaching.");

                Assert.That(
                    DimensionQueryCompanions.WhatItWatches(probe),
                    Is.EqualTo(5u),
                    "The later pass dropped a layer the earlier one asked for. The masks have to " +
                    "be added together, because each feature filters the list again by its own rule.");

                Assert.That(
                    DimensionQueryCompanions.ItLooksEveryFrame(probe),
                    Is.True,
                    "The later pass turned off checking every frame after an earlier one asked for it.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void SomethingThatMovesIsAllowedToTurnAndSomethingStillIsNot()
        {
            GameObject mover = new GameObject("MoverProbe");
            GameObject still = new GameObject("StillProbe");
            try
            {
                mover.AddComponent<AnimationAuthoring>();
                mover.AddComponent<ChaseStateAuthoring>();
                List<string> saidAboutTheMover = new List<string>();
                DimensionQueryCompanions.TurnsIfSomethingOnItNeedsTo(
                    mover, "mover", saidAboutTheMover.Add);

                Assert.That(
                    mover.GetComponent<AnimationAuthoring>().orientationSupport,
                    Is.Not.EqualTo(AnimationAuthoring.OrientationSupport.None),
                    "A creature that chases was left unable to turn. Every answer in " +
                    "DimensionQueryCompanions.AnswersThatOnlyWorkOnSomethingThatCanTurn is read " +
                    "by a system that names the facing component, the chase included, so it " +
                    "would chase nothing.");

                Assert.That(
                    saidAboutTheMover,
                    Is.Not.Empty,
                    "The facing answer was overruled without telling anybody.");

                still.AddComponent<AnimationAuthoring>();
                DimensionQueryCompanions.TurnsIfSomethingOnItNeedsTo(still, "still", null);

                Assert.That(
                    still.GetComponent<AnimationAuthoring>().orientationSupport,
                    Is.EqualTo(AnimationAuthoring.OrientationSupport.None),
                    "Something that does nothing but stand there was made to turn. 'Does not turn' " +
                    "is the right answer for scenery and a turret, and it is the author's to make.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mover);
                UnityEngine.Object.DestroyImmediate(still);
            }
        }

        /// <summary>
        /// The bare name a type argument ends in, with every legal decoration stripped.
        /// </summary>
        /// <remarks>
        /// <c>global::</c> and the verbatim <c>@</c> are both legal C# and both defeated the
        /// character class the scans used to be written with, so <c>Ensure&lt;global::Pug.X&gt;</c>
        /// and <c>Ensure&lt;@X&gt;</c> named nothing any list had to answer for. The tree already
        /// aliases a type in eight files to dodge a name clash, so a qualified name showing up is
        /// a matter of the next clash, not of anyone being clever.
        /// </remarks>
        private static string LastPartOf(string typeName)
        {
            string name = typeName.Trim();

            int qualifier = name.LastIndexOf("::", StringComparison.Ordinal);
            if (qualifier >= 0)
            {
                name = name.Substring(qualifier + 2);
            }

            int dot = name.LastIndexOf('.');
            if (dot >= 0)
            {
                name = name.Substring(dot + 1);
            }

            return name.StartsWith("@", StringComparison.Ordinal) ? name.Substring(1) : name;
        }

        private static Type FindAuthoringType(string name)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException loaded)
                {
                    types = loaded.Types;
                }

                for (int i = 0; i < types.Length; i++)
                {
                    if (types[i] != null &&
                        types[i].Name == name &&
                        typeof(Component).IsAssignableFrom(types[i]))
                    {
                        return types[i];
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Every source file in the framework that could put a component on an object.
        /// </summary>
        /// <remarks>
        /// EVERYTHING, not <c>Editor/Dimension*.cs</c> at the top level. That narrower scan read
        /// about a quarter of the framework: <c>Editor/Generation</c>, <c>Editor/UI</c>,
        /// <c>Scripts</c> and <c>API</c> were never opened, and a generator named anything but
        /// <c>Dimension*</c> was invisible. Four files in the widened part DO write Core Keeper
        /// components — <c>NullforgeDimensionService.RuntimeLoadingInternals.cs</c>,
        /// <c>DimensionItemPortalSpawnSystem.cs</c>, <c>DimensionPortalAuthoringConverter.cs</c>
        /// and <c>DimensionDungeonAssembler.cs</c> — so the remark that used to sit here saying
        /// nothing in those folders writes one was wrong, and widening the scan was not free: it
        /// is what put those four on the excused list with a reason. This test's own folder is
        /// left out, because the probes below add components on purpose.
        /// <para>
        /// WHERE IT STOPS. It roots at <c>Assets/ExpandNullforge</c>, so the two sibling folders
        /// under <c>Assets</c> — <c>Nullforge</c> and <c>MPTest</c>, each a built mod with its own
        /// assembly definition and one generated bootstrap in it — are never read. Both were
        /// checked: the only Core Keeper-looking text in either is a <c>using</c> line, and neither
        /// puts a component on anything. They are a mod's OUTPUT rather than this framework's
        /// source, which is why the root is where it is; a generator that ever moves out there
        /// would be invisible to every scan in this file.
        /// </para>
        /// </remarks>
        private static IEnumerable<string> GeneratorSources()
        {
            string root = Path.Combine(Application.dataPath, "ExpandNullforge");
            if (!Directory.Exists(root))
            {
                yield break;
            }

            string tests = Path.Combine(root, "Editor" + Path.DirectorySeparatorChar + "Tests");

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.StartsWith(tests, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return file;
            }
        }
    }
}
#endif
