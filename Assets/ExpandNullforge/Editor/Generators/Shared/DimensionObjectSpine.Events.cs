using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Event terminals and the seasonal machinery around them.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        /// <summary>
        /// Event terminals, the Cicada's kit, tanks and terrariums, fishing nets, farming machines,
        /// and the last odds and ends.
        /// </summary>
        public static void ApplyEventTerminal(
            GameObject root,
            DimensionEventTerminalTemplate world,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            if (root == null || world == null)
            {
                return;
            }

            if (world.IsAnEventTerminal && !world.TerminalHasNoSteps)
            {
                EventTerminalAuthoring terminal = EnsureComponent<EventTerminalAuthoring>(root);
                terminal.radius = world.Reaches;
                terminal.duration = world.RunsFor;
                terminal.loopIndex = world.LoopsBackToStep;

                LootTableID reward;
                if (!string.IsNullOrEmpty(world.RewardTableId) &&
                    DimensionEditorLootTables.TryResolve(world.RewardTableId, out reward))
                {
                    terminal.lootTable = reward;
                }
                else if (!string.IsNullOrEmpty(world.RewardTableId) && report != null)
                {
                    report(
                        "rewards from loot table '" + world.RewardTableId + "', which the game does " +
                        "not have, so finishing its event gives nothing.");
                }

                terminal.alwaysActiveConnections =
                    new System.Collections.Generic.List<EventTerminalAuthoring.AlwaysActiveConnection>();
                string[] alwaysOn = world.AlwaysOnConnections;
                for (int i = 0; i < alwaysOn.Length; i++)
                {
                    ConnectionAndDirection wire;
                    if (System.Enum.TryParse(alwaysOn[i], false, out wire))
                    {
                        terminal.alwaysActiveConnections.Add(
                            new EventTerminalAuthoring.AlwaysActiveConnection { connection = wire });
                    }
                    else if (report != null)
                    {
                        report(
                            "keeps connection '" + alwaysOn[i] + "' on for its whole event, which " +
                            "is not a connection the game has.");
                    }
                }

                terminal.eventSequence =
                    new System.Collections.Generic.List<EventTerminalAuthoring.EventTerminalSequence>();
                DimensionTerminalStep[] steps = world.Steps;
                for (int i = 0; i < steps.Length; i++)
                {
                    ConnectionAndDirection target;
                    System.Enum.TryParse(steps[i].Connection, false, out target);
                    terminal.eventSequence.Add(new EventTerminalAuthoring.EventTerminalSequence
                    {
                        action = (EventTerminalAction)(int)steps[i].Action,
                        target = target,
                        duration = steps[i].Seconds
                    });
                }
            }
            else
            {
                RemoveComponentIfPresent<EventTerminalAuthoring>(root);
            }

            if (world.FightsLikeTheCicada)
            {
                GiantCicadaBossAuthoring cicada = EnsureComponent<GiantCicadaBossAuthoring>(root);
                cicada.amountOfStages = world.Stages;
                cicada.lowestStageMultiplier = world.WeakestStage;
                cicada.stageTransitionDuration = world.StageChangeSeconds;
                cicada.armSlamDamage = world.ArmSlamDamage;
                cicada.damageMultiplier = world.ArmSlamMultiplier;
                cicada.armSlamAnticipation = world.ArmSlamWindUp;
                cicada.armSlamAnimationDuration = world.ArmSlamSeconds;
                cicada.armSlamCooldown = world.ArmSlamCooldown;
                cicada.spawnDuration = world.NymphSpawnSeconds;
                cicada.spawnNymphsMinCooldown = world.NymphMinCooldown;
                cicada.spawnNymphsMaxCooldown = world.NymphMaxCooldown;
                EnsureComponent<CicadaBossAuthoring>(root);
                cicada.voidSpawn = new GiantCicadaBossAuthoring.VoidSpawnConfiguration
                {
                    disabled = !world.CicadaSummonsVoid,
                    duration = world.CicadaVoidSeconds,
                    durationUntilSpawn = world.CicadaVoidWindUp,
                    durationAfterSpawn = world.CicadaVoidRecovery,
                    minCooldown = world.CicadaVoidMinCooldown,
                    maxCooldown = world.CicadaVoidMaxCooldown
                };
            }
            else
            {
                RemoveComponentIfPresent<GiantCicadaBossAuthoring>(root);
                RemoveComponentIfPresent<CicadaBossAuthoring>(root);
            }

            Toggle<CicadaNymphAuthoring>(root, world.IsACicadaNymph);

            if (world.HoldsAMiniWorld)
            {
                ContainedMiniSim.Authoring.ContainedMiniSimAuthoring mini =
                    EnsureComponent<ContainedMiniSim.Authoring.ContainedMiniSimAuthoring>(root);
                mini.maxNumberOfSimulatedElements = world.MiniWorldPopulation;
                mini.simulatedEntity = world.MiniWorldInhabitant;
                mini.simulateAreaMinMaxWidth = world.MiniWorldWidth;
                mini.simulateAreaMinMaxHeight = world.MiniWorldHeight;
                mini.simulateAreaMinMaxLength = world.MiniWorldLength;
            }
            else
            {
                RemoveComponentIfPresent<ContainedMiniSim.Authoring.ContainedMiniSimAuthoring>(root);
            }

            if (world.IsAFishingNet)
            {
                // SOMEWHERE TO PUT THE FISH. The pass that draws a net reads the net's own slots
                // to know what is in it, so a net with none is never drawn catching anything. The
                // game's own fishing net carries slots beside the visual answer.
                bool hadSomewhereToPutFish = HasNamed(root, "InventoryAuthoring");
                EnsureInventory(root);
                SayWhenTicked(
                    !hadSomewhereToPutFish,
                    report,
                    "is a fishing net with nowhere to keep what it catches, and the net is drawn " +
                    "from what is in it. Slots were filled in. Build it as a Container if you " +
                    "want to choose how many.");

                FishingNetVisualAuthoring net = EnsureComponent<FishingNetVisualAuthoring>(root);
                net.minMaxSplashTimerSingleFish = new Unity.Mathematics.float2(
                    world.SplashTimerOneFish.x,
                    world.SplashTimerOneFish.y);
                net.minMaxSplashTimerFullNet = new Unity.Mathematics.float2(
                    world.SplashTimerFull.x,
                    world.SplashTimerFull.y);

                net.visualSlots =
                    new System.Collections.Generic.List<FishingNetVisualAuthoring.Slot>();
                Vector2[] spots = world.FishPositions;
                for (int i = 0; i < spots.Length; i++)
                {
                    net.visualSlots.Add(new FishingNetVisualAuthoring.Slot
                    {
                        visualOffset = spots[i]
                    });
                }
            }
            else
            {
                RemoveComponentIfPresent<FishingNetVisualAuthoring>(root);
            }

            if (world.NatureTerritorySize > 0)
            {
                CavelingNatureTerritorySpawnerAuthoring nature =
                    EnsureComponent<CavelingNatureTerritorySpawnerAuthoring>(root);
                nature.size = world.NatureTerritorySize;
                nature.farmerSpawnChance = world.FarmerChance;
                nature.hunterSpawnChance = world.HunterChance;
            }
            else
            {
                RemoveComponentIfPresent<CavelingNatureTerritorySpawnerAuthoring>(root);
            }

            if (world.IsAnAutomatedPlanter && !world.FarmMachineWorksOnNothing)
            {
                AutomatedMoveAndPlanterAuthoring planter =
                    EnsureComponent<AutomatedMoveAndPlanterAuthoring>(root);
                planter.affectedPositions =
                    new System.Collections.Generic.List<
                        AutomatedMoveAndPlanterAuthoring.AffectedPositions>();
                DimensionFarmReach[] reach = world.WorksOn;
                for (int i = 0; i < reach.Length; i++)
                {
                    planter.affectedPositions.Add(
                        new AutomatedMoveAndPlanterAuthoring.AffectedPositions
                        {
                            position = new Unity.Mathematics.int2(reach[i].Tile.x, reach[i].Tile.y),
                            moveVector = new Unity.Mathematics.int2(
                                reach[i].MovesItToward.x,
                                reach[i].MovesItToward.y)
                        });
                }
            }
            else
            {
                RemoveComponentIfPresent<AutomatedMoveAndPlanterAuthoring>(root);
            }

            if (world.IsAnAutomatedHarvester && !world.FarmMachineWorksOnNothing)
            {
                AutomatedHarvestAndMoverAuthoring harvester =
                    EnsureComponent<AutomatedHarvestAndMoverAuthoring>(root);
                harvester.affectedPositions =
                    new System.Collections.Generic.List<
                        AutomatedHarvestAndMoverAuthoring.AffectedPositions>();
                DimensionFarmReach[] reach = world.WorksOn;
                for (int i = 0; i < reach.Length; i++)
                {
                    harvester.affectedPositions.Add(
                        new AutomatedHarvestAndMoverAuthoring.AffectedPositions
                        {
                            position = new Unity.Mathematics.int2(reach[i].Tile.x, reach[i].Tile.y),
                            moveVector = new Unity.Mathematics.int2(
                                reach[i].MovesItToward.x,
                                reach[i].MovesItToward.y)
                        });
                }
            }
            else
            {
                RemoveComponentIfPresent<AutomatedHarvestAndMoverAuthoring>(root);
            }

            // A FARM ARM MOVES THINGS, AND MOVING TAKES TIME. Both farm answers require the shared
            // moving block, so Unity attaches one — at nothing: no move time, no rest, and no
            // picking up. A move time of nothing is not "instant", it is a timer that fires every
            // tick with nothing to count down, and the arm plants and harvests as fast as the game
            // runs. The block is only filled in by the "it moves things" answer, which is a
            // different tick in a different place, and a planter is not obviously a mover to
            // anybody. The game's own farm arm carries real times on all of it.
            if ((world.IsAnAutomatedPlanter || world.IsAnAutomatedHarvester) &&
                !world.FarmMachineWorksOnNothing)
            {
                AutomatedMoverSharedAuthoring howItMoves =
                    EnsureComponent<AutomatedMoverSharedAuthoring>(root);
                if (howItMoves.moveTime <= 0f)
                {
                    howItMoves.moveTime = VanillaFarmArmMoveSeconds;
                    howItMoves.cooldownTime = VanillaFarmArmRestSeconds;
                    howItMoves.pickUpDuringMove = true;
                    howItMoves.allowPickupFromInventories = true;

                    SayWhenTicked(
                        true,
                        report,
                        "plants or harvests on its own, and how long a pass over a tile takes was " +
                        "not set — with nothing there the arm works as fast as the game runs. It " +
                        "was given the timing the game's own farm arm uses. Tick 'it moves things' " +
                        "under Automation if you want to choose the timing yourself.");
                }
            }

            // REFUSED, because writing it deletes the object. Core Keeper's water spreading runs
            // off a tile position stored on the component, and it is an ABSOLUTE position in the
            // world, not an offset from the object. Nothing here can know it, so it arrives as
            // 0,0 — and about two seconds after the object is placed the game looks at world tile
            // 0,0, finds no water and no pit there, and destroys the object. No prefab in Core
            // Keeper carries this: the game only ever makes a bare one-off marker at a tile it
            // already knows, which is not something an object can be.
            Toggle<WaterSpreaderAuthoring>(root, false);
            SayWhenTicked(
                world.SpreadsWater,
                report,
                "is set to spread water, and that was not written. Core Keeper spreads water from " +
                "a tile it is told about rather than from an object, and an object set to do it " +
                "deletes itself a couple of seconds after it is placed. Use watered ground, or a " +
                "sprinkler, instead.");

            if (world.IsAStandaloneRecipe && !world.RecipeMakesNothing)
            {
                ObjectID made = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(world.RecipeMakesId);
                if (made == ObjectID.None)
                {
                    RemoveComponentIfPresent<RecipeAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "is a recipe for '" + world.RecipeMakesId + "', which the game does not " +
                            "have, so it makes nothing.");
                    }
                }
                else
                {
                    RecipeAuthoring recipe = EnsureComponent<RecipeAuthoring>(root);
                    recipe.objectToCraft = new ObjectData
                    {
                        objectID = made,
                        variation = world.RecipeMakesVariation,
                        amount = world.RecipeMakesAmount
                    };
                    recipe.requiresNearbyObject =
                        string.IsNullOrEmpty(world.RecipeNeedsNearbyId) || resolveObject == null
                            ? ObjectID.None
                            : resolveObject(world.RecipeNeedsNearbyId);
                }
            }
            else
            {
                RemoveComponentIfPresent<RecipeAuthoring>(root);
            }

            if (world.UsesItsOwnPlacementIndicator)
            {
                EnsureComponent<PlacementIndicator.PlacementIndicatorAuthoring>(root).axisToSpeed =
                    world.PlacementIndicatorSpeed;
            }
            else
            {
                RemoveComponentIfPresent<PlacementIndicator.PlacementIndicatorAuthoring>(root);
            }

            SayWhenTicked(
                world.UsesItsOwnPlacementIndicator,
                report,
                "is set to use its own placement indicator. The indicator is the square that " +
                "follows the player's cursor, and Core Keeper only ever reads that off the player, " +
                "so an object of your own will not get one and nothing about placing it changes.");

            if (report == null)
            {
                return;
            }

            if (world.TerminalHasNoSteps)
            {
                report("is an event terminal with no steps, so its event finishes instantly.");
            }

            if (world.LoopsPastTheEnd)
            {
                report(
                    "loops back to a step past the end of its own sequence, so the loop points at " +
                    "nothing.");
            }

            if (world.FarmMachineWorksOnNothing)
            {
                report(
                    "is a farming machine with no tiles listed to work on, so it runs and touches " +
                    "nothing.");
            }

            if (world.RecipeMakesNothing)
            {
                report("is a recipe that makes nothing.");
            }
        }

        /// <summary>How long one pass of the game's own farm arm takes, and its rest after.</summary>
        /// <remarks>
        /// Used only as a floor when the author has not set a time at all, because nothing is not
        /// "instant" to the job that reads it — it is a timer with nothing to count.
        /// </remarks>
        private const float VanillaFarmArmMoveSeconds = 0.5f;

        /// <summary>The pause between passes.</summary>
        private const float VanillaFarmArmRestSeconds = 0.5f;
    }
}
