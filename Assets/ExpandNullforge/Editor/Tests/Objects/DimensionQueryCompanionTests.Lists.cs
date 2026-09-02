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
    /// The written verdicts: what has been read against its system, and what has not.
    /// </summary>
    internal sealed partial class DimensionQueryCompanionTests
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
        /// behind them is the same reading — which is why the summary above does not call every
        /// name in the list a Core Keeper component. Three of them are not.
        /// </para>
        /// <para>
        /// A NAME WITH A COMPANION ROW MAY NOT BE IN HERE.
        /// The two say opposite things — a row exists because that component's system needs
        /// something beside it — and with a name in both, deleting a row leaves the build green
        /// and the feature silently broken, which is the exact failure this file was written for.
        /// <see cref="NoNameSitsInMoreThanOneOfTheAnswerLists"/> makes that impossible.
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
                // "BreedStateAuthoring" belongs in two companion rows rather than here:
                // breeding needs a chase to walk to a mate with and an eat state to count
                // meals against, and a passive animal is given neither.
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
                // "RoamingStateAuthoring" belongs in a companion row rather than here: the
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
        /// Deciding this with a suffix test — dropping anything not ending in "Authoring" before
        /// any list is consulted — silently excuses every Core Keeper component
        /// whose class name has a different shape, and two whole classes of them are live:
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

        /// <summary>
        /// The census file the "verdict is recorded" list points at.
        /// </summary>
        /// <remarks>
        /// It is outside the Unity project, which is why it is easy to leave unread. Unread, the
        /// list's promise is unbacked: moving a name into it from the backlog
        /// leaves every test green, because the only thing checking that the verdict was
        /// written is the person writing it.
        /// </remarks>
        private const string TheCensus = @"E:\ck mods\ck-research\query-match-census.md";

        // ONE REASON WRITTEN ONCE, USED BY EVERY PART OF THE TYPE IT IS ABOUT. The list below is
        // keyed by file, so a type written across several files takes a row each; the reason is
        // still one reason, and copying the sentence out sixteen times would only make it easy to
        // change one copy and not the rest.

        /// <summary>Why no part of the spine sweeps.</summary>
        private const string TheSpineIsAHelper =
            "a shared helper, not a generator. The reason it is safe is narrower than this entry " +
            "used to claim: six files call into the spine and never sweep — the two blast " +
            "generators, the projectile generator, the block authoring, the runtime loader and " +
            "the world-rules generator — so 'whoever owns the object sweeps afterwards' is not " +
            "true of every caller. It holds because every spine method that writes a component a " +
            "companion row names — ApplyExplosive, ApplyChainReaction, ApplyObjectRules, and the " +
            "eight the creature generator was taught to call (ApplyContinuousAttack, " +
            "ApplyBeamAndAmbience, ApplyHidingAndHatching, ApplyManaAndAura, ApplyFinalTouches, " +
            "ApplySpawnerAndOrb, ApplyTrader, ApplyCreatureLastStand) — is reached only from the " +
            "item, world-object and creature generators, and all three sweep. That is a fact " +
            "about today's call graph and nothing checks it";

        /// <summary>Why no part of the interaction helper sweeps.</summary>
        private const string TheInteractionHelperIsAHelper =
            "a shared helper for the picture and the usable part, called from generators that " +
            "sweep afterwards";

        /// <summary>Why no part of the runtime bootstrap utility sweeps.</summary>
        private const string TheBootstrapEditsPrefabsAlreadyBuilt =
            "it edits prefabs the generators have already built and swept, and the portal " +
            "prefabs it builds itself have not been read against the rows";

        /// <summary>Why a pass of the creature generator does not sweep for itself.</summary>
        private const string TheCreatureTrunkSweepsAfterwards =
            "a pass of the creature generator. Configure calls it and then ends in " +
            "FinishACreature over the finished creature, so the sweep runs after everything " +
            "written here. If the trunk ever stops sweeping it fails on its own row, because it " +
            "writes surfaces too";

        /// <summary>Why a pass of the item generator does not sweep for itself.</summary>
        private const string TheItemTrunkSweepsAfterwards =
            "a pass of the item generator. Configure calls it, and the loop that called Configure " +
            "ends in CloseTheGaps before the prefab is saved";

        /// <summary>Why a pass of the world-object generator does not sweep for itself.</summary>
        private const string TheWorldObjectTrunkSweepsAfterwards =
            "a pass of the world-object generator. Configure calls it and then ends in " +
            "FinishAWorldObject over the finished object";

        /// <summary>Why the companion table's own fills do not sweep.</summary>
        private const string ItIsTheSweep =
            "it is the sweep. What it writes are the fills the rows promise, and CloseTheGaps and " +
            "the three Finish shorthands that call them are in DimensionQueryCompanions.Sweeps.cs";

        /// <summary>
        /// The files that put a Core Keeper surface on something and never run the sweep, each with
        /// the reason.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NOTHING ELSE CHECKS THAT THE SWEEP RAN AT ALL. Every row in the companion table only
        /// does anything if something calls <c>CloseTheGaps</c> over the finished object, and
        /// fifty files write Core Keeper surfaces without any call reaching them: two of the
        /// generators, the tileset block, ore and farming path, and
        /// the runtime bootstrap. Writing the list down turns "nobody
        /// checked" into "somebody said why", and the test below fails both ways: a new unswept
        /// file, and a name here that has started sweeping or stopped writing surfaces.
        /// </para>
        /// <para>
        /// A file counts as sweeping if it calls <c>CloseTheGaps</c> or one of the three
        /// <c>Finish…</c> helpers that ends in one.
        /// </para>
        /// <para>
        /// IT IS KEYED BY FILE AND THE FILE IS THE UNIT. Thirty-four of the rows arrived with the
        /// file split: the spine was one row and is sixteen, the runtime bootstrap one and is
        /// three, and the passes of the four generators that DO sweep answer one at a time now,
        /// because the sweep stayed in the trunk that calls them. Answering per TYPE instead was
        /// tried for one stage, and it made the single row against
        /// <c>NullforgeDimensionService.RuntimeLoadingInternals.cs</c> answer for all seventy-three
        /// files of that type. A part nobody wrote down is a part nobody answered for.
        /// </para>
        /// </remarks>
        private static readonly Dictionary<string, string> WritesASurfaceAndDoesNotSweep =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // THE SPINE, one row per part. The trunk is not here: since the split it declares
                // the type and writes nothing, so a row against it would be reported as stale.
                { "DimensionObjectSpine.Bosses.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Creatures.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Events.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Explosives.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Food.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Habits.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Items.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Loot.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Machines.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Music.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Placement.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Roles.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Summoning.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Terrain.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Traits.cs", TheSpineIsAHelper },
                { "DimensionObjectSpine.Weapons.cs", TheSpineIsAHelper },

                { "DimensionInteractionVisualUtility.cs", TheInteractionHelperIsAHelper },
                { "DimensionInteractionVisualUtility.Parts.cs", TheInteractionHelperIsAHelper },
                { "DimensionInteractionVisualUtility.Visual.cs", TheInteractionHelperIsAHelper },
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
                // THE RUNTIME BOOTSTRAP UTILITY, one row per part that writes. Its trunk is not
                // here for the same reason the spine's is not: it writes nothing now.
                {
                    "DimensionRuntimeConsumerBootstrapUtility.PortalObjects.cs",
                    TheBootstrapEditsPrefabsAlreadyBuilt
                },
                {
                    "DimensionRuntimeConsumerBootstrapUtility.PortalPrefab.cs",
                    TheBootstrapEditsPrefabsAlreadyBuilt
                },
                {
                    "DimensionRuntimeConsumerBootstrapUtility.Serialized.cs",
                    TheBootstrapEditsPrefabsAlreadyBuilt
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
                    "DimensionDungeonAssembler.Scenes.cs",
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

                // THE PASSES OF A GENERATOR THAT DOES SWEEP. Each of these was one file with its
                // generator until the split, and the sweep is still in the trunk that calls them.
                // The trunk writes surfaces of its own, so it answers here on its own footing:
                // take the sweep out of it and it is the one that fails.
                { "DimensionCreatureGenerator.Combat.cs", TheCreatureTrunkSweepsAfterwards },
                { "DimensionCreatureGenerator.Lifecycle.cs", TheCreatureTrunkSweepsAfterwards },
                { "DimensionCreatureGenerator.Temperament.cs", TheCreatureTrunkSweepsAfterwards },
                { "DimensionItemGenerator.Components.cs", TheItemTrunkSweepsAfterwards },
                { "DimensionItemGenerator.Localization.cs", TheItemTrunkSweepsAfterwards },
                { "DimensionWorldObjectGenerator.Behaviour.cs", TheWorldObjectTrunkSweepsAfterwards },
                { "DimensionWorldObjectGenerator.Placement.cs", TheWorldObjectTrunkSweepsAfterwards },
                {
                    "DimensionPlantGenerator.Configure.cs",
                    "the crop passes of the plant generator. They run as the configure step inside " +
                    "BuildPrefab, which ends in CloseTheGaps before the prefab is saved"
                },

                // THE SWEEP ITSELF. Before the split its trunk held the declaration of
                // CloseTheGaps, and the check — a text match for the call — read that as the file
                // sweeping. The declaration is in .Sweeps.cs now, so the four files that do the
                // filling say why here instead of passing on a coincidence.
                { "DimensionQueryCompanions.cs", ItIsTheSweep },
                { "DimensionQueryCompanions.Bodies.cs", ItIsTheSweep },
                { "DimensionQueryCompanions.Layers.cs", ItIsTheSweep },
                { "DimensionQueryCompanions.Rows.cs", ItIsTheSweep },
            };
    }
}
#endif
