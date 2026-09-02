using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The one-tickbox traits, and the components written after everything else.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        /// <summary>
        /// The thirty things an object simply is, or simply does. Every one of them is a component
        /// with no fields, so the whole method is on-or-off.
        /// </summary>
        public static void ApplySimpleTraits(
            GameObject root,
            DimensionSimpleTraitsTemplate traits,
            System.Action<string> report)
        {
            if (root == null || traits == null)
            {
                return;
            }

            // ---- being hit ----
            Toggle<AttackableWithMeleeAuthoring>(root, traits.CanBeHitWithAWeaponNotJustATool);
            Toggle<DontCountAsHitForAttackerAuthoring>(root, traits.HittingItDoesNotCountAsAHit);
            Toggle<IgnoreImmuneZoneAuthoring>(root, traits.ImmuneGroundDoesNotProtectIt);
            Toggle<DisableMapMarkerOnDeathAuthoring>(root, traits.ItsMapMarkerGoesWhenItDies);

            // ---- how it sits in the world ----
            Toggle<DontBlockDiggingAuthoring>(root, traits.DoesNotBlockDigging);
            Toggle<DisablePhysicsAuthoring>(root, traits.HasNoPhysics);
            Toggle<MotionSmoothing.Authoring.MotionSmoothingAuthoring>(root, traits.MovesSmoothly);
            Toggle<Pug.Conversion.DontNeedTransformAuthoring>(root, traits.HasNoPositionOfItsOwn);
            SayWhenTicked(
                traits.HasNoPositionOfItsOwn,
                report,
                "has no position of its own, and that takes it out of the world rather than just " +
                "off the grid. Everything the game does to an object — growing, burning, " +
                "watering, dropping, being hit, being drawn — starts from where the object is, so " +
                "none of it will happen. One thing in Core Keeper is built this way and it is a " +
                "bookkeeping entity nobody ever sees. Leave this off for anything a player meets.");
            SayWhenTicked(
                traits.MovesSmoothly,
                report,
                "is set to move smoothly. That needs a moving body with smoothing switched on, " +
                "which nothing this framework builds has, so the movement will look the same as " +
                "it does now. No object in Core Keeper uses it either.");
            Toggle<DestroyEntityIfPlacementNotValidAuthoring>(
                root,
                traits.DisappearsIfItsSpotStopsBeingValid);

            // REFUSED WHEN THE PICTURE HAS NOTHING TO USE, rather than written and crashed on. The
            // trigger answer is what gives an object a "use" — and Core Keeper's step that builds
            // the use reaches into the object's picture for its first usable part and takes it by
            // index, so on a picture with none the generate throws instead of the look failing to
            // flip. The door answer has been guarded this way since the sweep was written; this is
            // the same guard on the other route to the same component.
            bool triggerCanFlipTheLook =
                traits.ATriggerFlipsItsLook &&
                DimensionQueryCompanions.ThereIsSomethingToUseOnIt(root);
            Toggle<ChangeVariationTriggerAuthoring>(root, triggerCanFlipTheLook);
            SayWhenTicked(
                traits.ATriggerFlipsItsLook && !triggerCanFlipTheLook,
                report,
                "is set so that using it flips its look, but there is nothing on it a player can " +
                "walk up to and use. Set what using it does, and give it a picture, and the look " +
                "will flip.");
            Toggle<MergeDroppedItemAuthoring>(root, traits.DroppedCopiesMergeTogether);
            SayWhenTicked(
                traits.DroppedCopiesMergeTogether,
                report,
                "is set so dropped copies merge together. Core Keeper writes that mark down and " +
                "then never looks at it again — no object in the game uses it and no part of the " +
                "game reads it — so dropped copies will lie side by side as they do now.");
            Toggle<CustomScenePrefabAuthoring>(root, traits.IsPartOfAHandmadeRoom);
            SayWhenTicked(
                traits.IsPartOfAHandmadeRoom,
                report,
                "is marked as part of a handmade room. That takes it out of the list the game uses " +
                "to find an object by name, so nothing can spawn it, drop it or give it to a " +
                "player any more. Only tick it on a copy that exists purely to be placed inside a " +
                "handmade room.");

            // ---- what it is ----
            if (traits.IsATrashCan)
            {
                EnsureInventory(root);
            }

            Toggle<TrashCanAuthoring>(root, traits.IsATrashCan);
            Toggle<SprinklerAuthoring>(root, traits.IsASprinkler);
            Toggle<BaitOnAPoleAuthoring>(root, traits.IsBaitOnAPole);
            Toggle<CherryBlossomTreeAuthoring>(root, traits.IsACherryTree);
            Toggle<FishShoalAuthoring>(root, traits.IsAShoalOfFish);
            SayWhenTicked(
                traits.IsAShoalOfFish,
                report,
                "is marked as a shoal of fish. Core Keeper writes that mark and nothing in the " +
                "game ever reads it, so the object will behave exactly as it does now.");
            Toggle<ContainedMiniSim.Authoring.ContainedMiniSimElementAuthoring>(
                root,
                traits.CanLiveInATank);
            Toggle<GrowingPlantAuthoring>(root, traits.IsAGrowingPlant);
            SayWhenTicked(
                traits.IsAGrowingPlant,
                report,
                "is marked as a growing plant. Nothing in Core Keeper reads that mark — growing is " +
                "done by the crop stages on the Plant asset — so on its own it changes nothing.");
            Toggle<TrailAuthoring>(root, traits.IsATrailSomethingLeftBehind);
            SayWhenTicked(
                traits.IsATrailSomethingLeftBehind && !HasNamed(root, "AttackContinuouslyAuthoring"),
                report,
                "is a trail something left behind, but a trail in Core Keeper is only ever a patch " +
                "of ground that keeps hurting whatever stands in it. Without 'it keeps attacking " +
                "whatever is near it' the trail is scenery. The game's own crystal spike trail and " +
                "void club trail both carry it.");

            // ---- creatures and pets ----
            Toggle<BreedToggleAuthoring>(root, traits.CanBeBred);
            Toggle<PheromoneSensorAuthoring>(root, traits.FollowsPheromoneTrails);
            Toggle<PetOwnerAuthoring>(root, traits.IsAPetsHome);
            Toggle<PetDataAuthoring>(root, traits.CarriesAPetsLook);
            SayWhenTicked(
                traits.CarriesAPetsLook,
                report,
                "carries a pet's look. Core Keeper keeps a pet's look and talents on a hidden " +
                "record beside the player's bag, not on the pet, and reads it from there — so on " +
                "an object of your own nothing looks at it.");
            Toggle<MinionDataAuthoring>(root, traits.IsAMinion);
            SayWhenTicked(
                traits.IsAMinion,
                report,
                "is marked as a minion by this tick, and that particular mark has no effect at " +
                "all: Core Keeper never turns it into anything. Use the real minion answer " +
                "instead — 'If it belongs to somebody' on a creature, or the object's roles on a " +
                "placed thing — on something that also has an attack.");

            // ---- player things ----
            Toggle<SoulsAuthoring>(root, traits.KeepsSouls);
            SayWhenTicked(
                traits.KeepsSouls,
                report,
                "is set to keep souls. Souls are read off the player and nowhere else, so on " +
                "anything but a player this collects nothing.");
            Toggle<PlayingInstrumentAuthoring>(root, traits.CanPlayInstruments);
            SayWhenTicked(
                traits.CanPlayInstruments,
                report,
                "is set to play instruments. The music the game plays back is read off players " +
                "only, so this object will hold a tune nobody hears.");
            if (traits.WearsEquipment)
            {
                EnsureInventory(root);
            }

            Toggle<EquipmentAuthoring>(root, traits.WearsEquipment);
            SayWhenTicked(
                traits.WearsEquipment,
                report,
                "is set to wear equipment. Only a player wears equipment in Core Keeper — the " +
                "helmets, hands and presets are all read off the player — so this adds about " +
                "fifty empty slots and four extra bags to the object and changes nothing else.");
            Toggle<CombatantsTrackerAuthoring>(root, traits.RemembersWhoItFights);
            SayWhenTicked(
                traits.RemembersWhoItFights && !HasNamed(root, "NearbyEntitiesTrackerAuthoring"),
                report,
                "is set to remember who it fights, but it cannot see anything near it, and the " +
                "list it would remember is built from what it sees. Give it a notice range.");

            // REFUSED. There is one achievement tracker in a Core Keeper world and the game asks
            // for it by expecting exactly one: a second one makes that ask throw, every frame, for
            // the rest of the session, and achievements stop for everybody in the world — not just
            // for the mod. It is not a gap that can be closed, so the component is not written —
            // and it is actively taken off, because a prefab made before this pass has one.
            Toggle<AchievementTrackerAuthoring>(root, false);
            SayWhenTicked(
                traits.CountsTowardsAchievements,
                report,
                "is set to count towards achievements, and that was not written. Core Keeper keeps " +
                "one achievement record per world and expects to find exactly one: a second stops " +
                "achievements working for everyone in that world, including the game's own. If " +
                "you want killing something to unlock an achievement, use 'triggers an achievement " +
                "when it dies' on a creature instead.");
            if (traits.CanCarryAffixes)
            {
                // Affixes are conditions, and the conditions component crashes on a bare add.
                EnsureSupportsConditions(root);
            }

            Toggle<Affixes.Authoring.SupportAffixesAuthoring>(root, traits.CanCarryAffixes);
            Toggle<OverrideLegendaryForSlotRequirementsAuthoring>(
                root,
                traits.LegendariesIgnoreSlotRulesHere);
            Toggle<TriggerEffectAuthoring>(root, traits.TheTriggersPushBackWhenUsed);
            SayWhenTicked(
                traits.TheTriggersPushBackWhenUsed,
                report,
                "is set to push back through the controller triggers. That part of Core Keeper is " +
                "switched off in this build — the two places that would apply and undo the effect " +
                "are empty and nothing calls them — so no controller will do anything.");

            // ---- being sent over the network ----
            //
            // REFUSED, both of them, for the same reason as the achievement record. Core Keeper
            // keeps one of each of these per player connection and asks for it by expecting
            // exactly one; a second makes that ask throw every frame and the client's biome
            // sampling or its slice of the map stops working for the whole session. They are taken
            // off actively, because a prefab made before this pass has one.
            Toggle<ClientBiomeSamplesAuthoring>(root, false);
            SayWhenTicked(
                traits.EachPlayerGetsItsOwnBiomeSamples,
                report,
                "asks for its own biome samples, and that was not written. Core Keeper keeps one " +
                "set per player and expects to find exactly one: a second stops the map reading " +
                "biomes at all for whoever is playing.");
            Toggle<ClientSubMapAuthoring>(root, false);
            SayWhenTicked(
                traits.EachPlayerGetsItsOwnSliceOfTheMap,
                report,
                "asks for its own slice of the map, and that was not written. Core Keeper picks " +
                "one object in the whole game to hold that, so a second one makes the choice a " +
                "coin toss and the map can end up drawn from the wrong thing.");
            Toggle<ConvertToInterpolatedGhostAfterSpawnAuthoring>(
                root,
                traits.SmoothsItselfOnceItHasSpawned);
            SayWhenTicked(
                traits.SmoothsItselfOnceItHasSpawned,
                report,
                "is set to smooth itself once it has spawned. That only happens to something the " +
                "game predicts on the player's own machine, which nothing this framework builds " +
                "is — everything it makes is already the smoothed kind — so nothing changes.");
            Toggle<CreateCharacterGuidAuthoring>(root, traits.GetsACharacterIdOfItsOwn);
            Toggle<CreatePlayerGuidAuthoring>(root, traits.GetsAPlayerIdOfItsOwn);

            if (traits.IsAFloatingDamageNumber)
            {
                // PugDamageAuthoring requires a ghost, because a damage number only means anything
                // once everyone in the game can see it.
                EnsureComponent<Unity.NetCode.GhostAuthoringComponent>(root);
            }

            Toggle<PugDamageAuthoring>(root, traits.IsAFloatingDamageNumber);
            SayWhenTicked(
                traits.IsAFloatingDamageNumber,
                report,
                "is marked as a floating damage number. Core Keeper has no step that turns that " +
                "mark into anything, and no object in the game carries it, so the object will not " +
                "become a damage number. Damage numbers come from the game's own set.");

            // ---- the debug map ----
            SayWhenTicked(
                traits.ShowItOnTheDebugMap,
                report,
                "is set to show on the debug map. The step in Core Keeper that would read the " +
                "marker, its colour and its size is empty in this build, so nothing will be drawn.");
            if (traits.ShowItOnTheDebugMap)
            {
                WorldExplorerDebugTrackerAuthoring tracker =
                    EnsureComponent<WorldExplorerDebugTrackerAuthoring>(root);
                tracker.markerType = traits.DrawnAsACircle
                    ? WorldExplorerDebugMarkerType.Circle
                    : WorldExplorerDebugMarkerType.Box;
                tracker.color = traits.DebugMapColour;
                tracker.radius = traits.DebugMapRadius;
                tracker.showEntityName = traits.DebugMapShowsItsName;
                tracker.showWhenDisabled = traits.DebugMapShowsItWhileSwitchedOff;
                tracker.colorWhenDisabled = traits.DebugMapSwitchedOffColour;
            }
            else
            {
                RemoveComponentIfPresent<WorldExplorerDebugTrackerAuthoring>(root);
            }
            if (report != null && traits.CherryTreeInAHandmadeRoomIsNotCounted)
            {
                report(
                    "is a cherry tree that is also part of a handmade room, and the game skips " +
                    "handmade-room trees when it counts them, so it counts towards nothing.");
            }

            if (report != null && traits.DebugMarkIsInvisible)
            {
                report(
                    "is drawn on the debug map with no size at all, so the mark it leaves there " +
                    "cannot be seen.");
            }
        }

        public static void ApplyFinalTouches(
            GameObject root,
            DimensionFinalTouchesTemplate world,
            System.Action<string> report)
        {
            if (root == null || world == null)
            {
                return;
            }

            if (world.CanBeOccupied)
            {
                OccupiableAuthoring occupiable = EnsureComponent<OccupiableAuthoring>(root);
                occupiable.occupiableSlots =
                    new System.Collections.Generic.List<OccupiableAuthoring.OccupiableSlot>
                    {
                        new OccupiableAuthoring.OccupiableSlot
                        {
                            offsetForward = ToFloat3(world.OccupantForward),
                            offsetRight = ToFloat3(world.OccupantRight),
                            offsetBack = ToFloat3(world.OccupantBack),
                            offsetLeft = ToFloat3(world.OccupantLeft)
                        }
                    };
            }
            else
            {
                RemoveComponentIfPresent<OccupiableAuthoring>(root);
            }

            if (world.ReactsToItsOwnWounds && !world.ReactsWithNothing)
            {
                ConditionID reaction;
                if (System.Enum.TryParse(world.ReactionConditionId, false, out reaction))
                {
                    ChanceToApplyConditionToSelfWhenDamagedAuthoring reacts =
                        EnsureComponent<ChanceToApplyConditionToSelfWhenDamagedAuthoring>(root);
                    reacts.conditionsByChance =
                        new System.Collections.Generic.List<
                            ChanceToApplyConditionToSelfWhenDamagedAuthoring.ConditionByChance>
                        {
                            new ChanceToApplyConditionToSelfWhenDamagedAuthoring.ConditionByChance
                            {
                                chanceForEachPercentDamageTakenByCurrentHealthPercentage =
                                    world.ReactionChanceByHealth,
                                conditionData = new ConditionData
                                {
                                    conditionID = reaction,
                                    duration = world.ReactionSeconds,
                                    value = world.ReactionStrength,
                                    valueMultiplier = 1f
                                }
                            }
                        };
                }
                else
                {
                    RemoveComponentIfPresent<ChanceToApplyConditionToSelfWhenDamagedAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "reacts to being hurt with '" + world.ReactionConditionId + "', which " +
                            "the game does not have, so nothing happens when it is hurt.");
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<ChanceToApplyConditionToSelfWhenDamagedAuthoring>(root);
            }

            if (world.DestructibleAboveHealth > 0f)
            {
                EnsureComponent<ActAsDestructibleWhileAboveHealthThresholdAuthoring>(root)
                    .threshold = world.DestructibleAboveHealth;

                // The label says destructible, and the second half of what the game does with it
                // is the half that will surprise somebody. Only one thing in Core Keeper carries
                // this and it is a monster pretending to be a rock, which is exactly the trick,
                // but a decoration set this way turns into a monster the first time it is hit.
                SayWhenTicked(
                    true,
                    report,
                    "counts as destructible only above a share of its health, and below that " +
                    "share Core Keeper turns it into an enemy: it becomes hostile, it stops being " +
                    "mineable, and it loses its damage reduction. That is the trick the game's own " +
                    "crystal snail plays. If you only wanted something breakable, take the share " +
                    "back to zero.");
            }
            else
            {
                RemoveComponentIfPresent<ActAsDestructibleWhileAboveHealthThresholdAuthoring>(root);
            }

            if (world.DiesWithItsOwner)
            {
                EnsureComponent<BeDestroyedAlongWithOwnerAuthoring>(root).owner = world.DiesWith;
            }
            else
            {
                RemoveComponentIfPresent<BeDestroyedAlongWithOwnerAuthoring>(root);
            }

            string[] scents = world.GivesOffPheromones;
            if (scents.Length > 0)
            {
                PheromoneAdderAuthoring scent = EnsureComponent<PheromoneAdderAuthoring>(root);
                scent.pheromones = new System.Collections.Generic.List<PheromoneType>();
                for (int i = 0; i < scents.Length; i++)
                {
                    PheromoneType kind;
                    if (System.Enum.TryParse(scents[i], false, out kind))
                    {
                        scent.pheromones.Add(kind);
                    }
                    else if (report != null)
                    {
                        report(
                            "gives off '" + scents[i] + "', which is not a pheromone the game has, " +
                            "so nothing will smell it.");
                    }
                }

                if (scent.pheromones.Count == 0)
                {
                    RemoveComponentIfPresent<PheromoneAdderAuthoring>(root);
                }
            }
            else
            {
                RemoveComponentIfPresent<PheromoneAdderAuthoring>(root);
            }

            ConditionID barred;
            if (!string.IsNullOrEmpty(world.ShowsConditionAsBar) &&
                System.Enum.TryParse(world.ShowsConditionAsBar, false, out barred))
            {
                EnsureComponent<DisplayConditionAsBarWhenEquippedAuthoring>(root).conditionID =
                    barred;

                SayWhenTicked(
                    true,
                    report,
                    "shows an effect as a bar while it is equipped. The bar is drawn from what a " +
                    "player is holding, so it will show for a weapon or a tool and not for " +
                    "something placed in the world.");
            }
            else
            {
                RemoveComponentIfPresent<DisplayConditionAsBarWhenEquippedAuthoring>(root);
            }

            EnvironmentEventType worldEvent;
            if (!string.IsNullOrEmpty(world.TriggersEventOnDeath) &&
                System.Enum.TryParse(world.TriggersEventOnDeath, false, out worldEvent))
            {
                EnsureComponent<EnvironmentEvents.Authoring.TriggerEnvironmentEventOnDeathAuthoring>(root)
                    .environmentEvent =
                    worldEvent;
            }
            else
            {
                RemoveComponentIfPresent<EnvironmentEvents.Authoring.TriggerEnvironmentEventOnDeathAuthoring>(root);
            }

            if (world.VisualFollowSpeed > 0f)
            {
                EnsureComponent<VisualSmoothFollowAuthoring>(root).speed = world.VisualFollowSpeed;
            }
            else
            {
                RemoveComponentIfPresent<VisualSmoothFollowAuthoring>(root);
            }

            if (world.WarmUpSeconds > 0f)
            {
                EnsureComponent<WarmupAuthoring>(root).warmup = world.WarmUpSeconds;

                SayWhenTicked(
                    true,
                    report,
                    "has a warm-up. Core Keeper only counts a warm-up down on something a player " +
                    "is holding — the minigun is the one thing in the game with one — so on a " +
                    "placed object nothing waits.");
            }
            else
            {
                RemoveComponentIfPresent<WarmupAuthoring>(root);
            }

            if (world.IsABossBeam)
            {
                BirdBossBeamAuthoring beam = EnsureComponent<BirdBossBeamAuthoring>(root);
                beam.startDuration = world.BeamStartSeconds;
                beam.loopDuration = world.BeamHoldSeconds;
                beam.endDuration = world.BeamEndSeconds;
                beam.hiddenEndDuration = world.BeamHiddenSeconds;
                beam.startDamageDelay = world.BeamHarmlessFor;
                beam.moveSpeed = world.BeamTravelSpeed;
                beam.moveSideWays = world.BeamSweepsSideways;
                beam.moveDirection = ToFloat3(world.BeamTravelDirection);

                CoreBossBeamAuthoring core = EnsureComponent<CoreBossBeamAuthoring>(root);
                core.startDuration = world.BeamStartSeconds;
                core.loopDuration = world.BeamHoldSeconds;
                core.endDuration = world.BeamEndSeconds;
                core.hiddenEndDuration = world.BeamHiddenSeconds;

                // A HEALTH POOL, EVEN WHEN NOTHING CAN ATTACK IT. Both beam systems name health as
                // something they WRITE — it is how the beam grows, holds and fades — so a beam
                // with no pool is skipped by both and never appears. "Cannot be attacked" is the
                // natural answer for a beam, and it takes the pool off. Core Keeper's own bird
                // boss beam and core boss beam each carry "cannot be attacked" and a health pool
                // together, which is exactly this pairing.
                GiveItBackTheHealthAnUnbreakableThingStillNeeds(
                    root,
                    report,
                    "is a boss beam that also cannot be attacked, and a beam grows and fades " +
                    "through its own health — with none it never appears at all. It was generated " +
                    "with a small pool, which is what the game's own boss beams carry alongside " +
                    "the same setting. Nothing can hurt it: it is not mineable and it has no " +
                    "hurt state.");

                // AND SOMETHING FOR IT TO HURT. The bird beam's system names the "keeps attacking
                // whatever is near it" component for writing, so a beam without it is skipped even
                // with a pool. Both of the game's beams carry it.
                if (!HasNamed(root, "AttackContinuouslyAuthoring"))
                {
                    EnsureComponent<AttackContinuouslyAuthoring>(root);
                    SayWhenTicked(
                        true,
                        report,
                        "is a boss beam, and a beam in Core Keeper is a thing that keeps hurting " +
                        "whatever stands in it. That answer was filled in, because the beam is " +
                        "skipped without it. Set what it attacks and how hard under the attack " +
                        "block.");
                }
            }
            else
            {
                RemoveComponentIfPresent<BirdBossBeamAuthoring>(root);
                RemoveComponentIfPresent<CoreBossBeamAuthoring>(root);
            }

            if (world.IsABossSpawnPoint)
            {
                CoreBossSpawnAuthoring spawn = EnsureComponent<CoreBossSpawnAuthoring>(root);
                spawn.distanceToPlayerToActivate = world.WakesWithin;
                spawn.distanceToPlayerToSpawn = world.SpawnsWithin;
                spawn.spawnTime = world.SpawnSeconds;
                spawn.destructionTime = world.BreakDownSeconds;
                spawn.spawnZOffset = world.SpawnHeight;

                SayWhenTicked(
                    !HasNamed(root, "SpawnCompanionsAuthoring"),
                    report,
                    "is a boss spawn point but has nothing listed to spawn beside it, and Core " +
                    "Keeper spawns a boss from that list. Nothing will come out of it. The " +
                    "companions list is a creature answer, so build the spawn point as a creature " +
                    "if you want it to bring something with it, the way the game's crystal meteor " +
                    "does.");
            }
            else
            {
                RemoveComponentIfPresent<CoreBossSpawnAuthoring>(root);
            }

            if (world.IsAHiveEgg)
            {
                LarvaHiveEggHatchStateAuthoring egg =
                    EnsureComponent<LarvaHiveEggHatchStateAuthoring>(root);
                egg.stateTransitionDuration = world.HiveEggChangeSeconds;
                egg.hatchDuration = world.HiveEggHatchSeconds;

                // Hatching is written over the egg's health, so an egg that cannot be attacked and
                // therefore has no pool is skipped and never hatches.
                GiveItBackTheHealthAnUnbreakableThingStillNeeds(
                    root,
                    report,
                    "hatches like a hive egg and also cannot be attacked, and the game hatches it " +
                    "through its own health — with none it sits there. It was generated with a " +
                    "small pool. Nothing can hurt it: it is not mineable and it has no hurt state.");
            }
            else
            {
                RemoveComponentIfPresent<LarvaHiveEggHatchStateAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (world.EverySeatIsTheSameSpot)
            {
                report(
                    "can be occupied but seats its occupant at dead centre whichever way it faces, " +
                    "which for anything wider than one tile puts them inside it.");
            }

            if (world.ReactsWithNothing)
            {
                report("reacts to its own wounds without naming a condition to apply.");
            }

            if (world.WakesCloserThanItSpawns)
            {
                report(
                    "wakes closer than it spawns, so a player is already inside its spawn range " +
                    "before it notices them.");
            }
        }

        public static void ApplyManaAndAura(
            GameObject root,
            DimensionManaAndAuraTemplate world,
            System.Func<string, ObjectID> resolveObject,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || world == null)
            {
                return;
            }

            if (world.HoldsMana)
            {
                ManaAuthoring mana = EnsureComponent<ManaAuthoring>(root);
                mana.startMana = world.StartingMana;
                mana.maxMana = world.MaximumMana;
                mana.manaTickRate = world.RefillRate;
                mana.startRegenDelay = world.RefillDelay;
            }
            else
            {
                RemoveComponentIfPresent<ManaAuthoring>(root);
            }

            SayWhenTicked(
                world.HoldsMana,
                report,
                "holds mana. The pool will refill the way you set it, but the barrier the game " +
                "spends mana on only works on something that records when it was last hurt, and " +
                "Core Keeper records that for players only. Mana on anything else is a number that " +
                "goes up.");

            SayWhenTicked(
                world.SiphonsMana && world.OwnedBy == null,
                report,
                "siphons mana, but Core Keeper only siphons on behalf of whoever owns the thing " +
                "doing it, and this has no owner. Set who owns it, under the same block, and the " +
                "siphon will run.");

            if (world.SiphonsMana)
            {
                SiphonMana.Authoring.SiphonManaAuthoring siphon =
                    EnsureComponent<SiphonMana.Authoring.SiphonManaAuthoring>(root);
                siphon.maxManaSiphonedPerSecond = world.SiphonRate;
                siphon.manaSiphonCooldownSeconds = world.SiphonCooldown;
                siphon.maxTransferDistance = world.SiphonReach;
                siphon.siphonRadius = world.SiphonRadius;
            }
            else
            {
                RemoveComponentIfPresent<SiphonMana.Authoring.SiphonManaAuthoring>(root);
            }

            if (world.HealsWhatIsNear)
            {
                HealNearbyEntitiesAuthoring heal =
                    EnsureComponent<HealNearbyEntitiesAuthoring>(root);
                heal.isActive = world.HealingStartsOn;
                heal.healthPerSecond = world.HealthPerSecond;
                heal.radius = world.HealRadius;

                FactionID side;
                if (!string.IsNullOrEmpty(world.HealsFaction) &&
                    System.Enum.TryParse(world.HealsFaction, false, out side))
                {
                    heal.healsTargetsOfFaction = side;
                }
                else if (!string.IsNullOrEmpty(world.HealsFaction) && report != null)
                {
                    report(
                        "heals faction '" + world.HealsFaction + "', which the game does not have, " +
                        "so it heals everyone nearby.");
                }
            }
            else
            {
                RemoveComponentIfPresent<HealNearbyEntitiesAuthoring>(root);
            }

            if (world.IsAncientWiring)
            {
                AncientElectricityConnectionAuthoring ancient =
                    EnsureComponent<AncientElectricityConnectionAuthoring>(root);
                ancient.electricityAmount = world.CarriesPower;
                ancient.sourceEnergy = world.ProducesPower;
                ancient.blocksElectricity = world.BlocksTheFlow;
            }
            else
            {
                RemoveComponentIfPresent<AncientElectricityConnectionAuthoring>(root);
            }

            if (world.IsPartOfSomethingElse)
            {
                EnsureComponent<EntityPartAuthoring>(root).mainEntity = world.BelongsTo;
            }
            else
            {
                RemoveComponentIfPresent<EntityPartAuthoring>(root);
            }

            if (world.OwnedBy != null)
            {
                EnsureComponent<OwnerAuthoring>(root).owner = world.OwnedBy;
            }
            else
            {
                RemoveComponentIfPresent<OwnerAuthoring>(root);
            }

            if (world.IsHydraBait)
            {
                EnsureComponent<HydraBossBaitAuthoring>(root).attractsHydraType =
                    (HydraBossType)(int)world.AttractsHydra;
            }
            else
            {
                RemoveComponentIfPresent<HydraBossBaitAuthoring>(root);
            }

            if (world.MarksABossSpawn && !world.MarksNoBoss)
            {
                ObjectID boss = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.BossThatAppearsId);
                if (boss == ObjectID.None)
                {
                    RemoveComponentIfPresent<BossSpawnLocationAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "marks where '" + world.BossThatAppearsId + "' appears, which the game " +
                            "does not have, so nothing will appear there.");
                    }
                }
                else
                {
                    EnsureComponent<BossSpawnLocationAuthoring>(root).bossID = boss;
                }
            }
            else
            {
                RemoveComponentIfPresent<BossSpawnLocationAuthoring>(root);
            }

            if (world.BlastLaysGround && !world.BlastGroundIsMissing)
            {
                int laid = ResolveTilesetName(world.BlastGroundTilesetId, resolveTileset);
                if (laid < 0)
                {
                    RemoveComponentIfPresent<SpawnTileOnExplosionAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "lays '" + world.BlastGroundTilesetId + "' where it explodes, which is " +
                            "not a tileset, so it leaves the ground as it was.");
                    }
                }
                else
                {
                    SpawnTileOnExplosionAuthoring blast =
                        EnsureComponent<SpawnTileOnExplosionAuthoring>(root);
                    blast.tileset = (PugTilemap.Tileset)laid;
                    blast.tileType = world.BlastGroundKind;
                    blast.duration = world.BlastGroundSeconds;
                    blast.spawnRequiresWalkable = world.BlastNeedsWalkableGround;
                }
            }
            else
            {
                RemoveComponentIfPresent<SpawnTileOnExplosionAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (world.SiphonsFromNowhere)
            {
                report("siphons mana with no reach and no radius, so it never draws from anything.");
            }

            if (world.HealsNothing)
            {
                report("heals what is near it for nothing per second.");
            }

            if (world.AncientWiringDoesNothing)
            {
                report(
                    "is ancient wiring that carries no power, produces none and blocks nothing, so " +
                    "it does nothing at all in the network.");
            }

            if (world.MarksNoBoss)
            {
                report("marks a boss spawn without naming which boss.");
            }

            if (world.BlastGroundIsMissing)
            {
                report("is told to lay ground where it explodes without naming any.");
            }
        }
    }
}
