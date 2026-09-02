using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Wiring, power, conveyors and the rest of what makes an object a machine.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        /// <summary>
        /// What an object does with electricity, if anything.
        /// </summary>
        /// <remarks>
        /// The whole of Core Keeper wiring is one seven-field component and a lot of systems reading
        /// it. Removed rather than left behind when an object stops being wired, or it keeps blocking
        /// current in a circuit nothing in the asset still describes.
        /// </remarks>
        public static void ApplyWiring(GameObject root, DimensionWiringTemplate wiring)
        {
            if (wiring == null || !wiring.IsWired)
            {
                RemoveComponentIfPresent<Pug.Automation.ElectricityAuthoring>(root);
                return;
            }

            Pug.Automation.ElectricityAuthoring electricity =
                EnsureComponent<Pug.Automation.ElectricityAuthoring>(root);
            electricity.sourceEnergy = wiring.PowerProduced;
            electricity.blocksElectricity = wiring.BlocksCurrent;
            electricity.direction = wiring.Direction;
            electricity.isLever = wiring.IsSwitch;
            electricity.isWire = wiring.IsWire;
            electricity.circuitConnectionMode = (CircuitConnectionMode)(int)wiring.Shape;

            if (wiring.Role == DimensionWiringRole.LogicCircuit)
            {
                electricity.circuitType = wiring.DelaysInsteadOfConditions
                    ? CircuitType.Delay
                    : CircuitType.Condition;
            }
            else
            {
                electricity.circuitType = CircuitType.None;
            }
        }

        /// <summary>
        /// The push a conveyor gives to whatever stands on it.
        /// </summary>
        /// <remarks>
        /// One direction per variation, because a belt with four rotations is one object with four
        /// variations rather than four objects. A belt with no directions runs and moves nothing.
        /// </remarks>
        private static void ApplyConveyorPush(
            GameObject root,
            DimensionAutomationTemplate automation,
            System.Action<string> report)
        {
            if (!automation.PushesWhatStandsOnIt)
            {
                RemoveComponentIfPresent<VelocityAffectorAuthoring>(root);
                return;
            }

            if (automation.PushesNowhere && report != null)
            {
                report(
                    "pushes what stands on it and was never told which way, so it runs and moves " +
                    "nothing.");
            }

            VelocityAffectorAuthoring push = EnsureComponent<VelocityAffectorAuthoring>(root);
            push.priority = automation.PushPriority;
            push.requiresElectricity = automation.PushNeedsPower;
            push.moveForceOptions =
                new System.Collections.Generic.List<VelocityAffectorAuthoring.MoveForceOption>();

            Vector2Int[] directions = automation.PushDirections;
            for (int i = 0; i < directions.Length; i++)
            {
                push.moveForceOptions.Add(new VelocityAffectorAuthoring.MoveForceOption
                {
                    moveForce = new Unity.Mathematics.int2(directions[i].x, directions[i].y)
                });
            }
        }

        private static void ApplyEntrance(
            GameObject root,
            DimensionCreatureLifecycleTemplate lifecycle,
            System.Action<string> report)
        {
            if (!lifecycle.MakesAnEntrance)
            {
                RemoveComponentIfPresent<SpawnStateAuthoring>(root);
                return;
            }

            if (lifecycle.MakesAnEntranceWithNoAnimation && report != null)
            {
                report(
                    "makes an entrance with no animation to make it with, so it stands still for " +
                    lifecycle.EntranceSeconds + " seconds and then acts.");
            }

            SpawnStateAuthoring entrance = EnsureComponent<SpawnStateAuthoring>(root);
            entrance.duration = lifecycle.EntranceSeconds;
            entrance.animId = lifecycle.EntranceAnimation;
            entrance.removeTilesOnSpawn = lifecycle.ClearsTheGroundAsItArrives;
            entrance.radiusToRemoveTilesWithin =
                lifecycle.ClearsTheGroundAsItArrives ? lifecycle.ClearedRadius : 0f;
            entrance.facingDirection = new Unity.Mathematics.float2(
                lifecycle.FacesOnEntrance.x,
                lifecycle.FacesOnEntrance.y);
            entrance.removeTilesOnSpawnOffset = new Unity.Mathematics.float2(
                lifecycle.ClearsTilesOffsetBy.x,
                lifecycle.ClearsTilesOffsetBy.y);
        }

        /// <summary>
        /// What Core Keeper's automation can do with a thing.
        /// </summary>
        /// <remarks>
        /// Five markers and one settings block. The settings only mean anything on the thing doing
        /// the moving, so they are written only there — a chest with mover timings on it would be a
        /// component the game reads and nothing acts on.
        /// </remarks>
        public static void ApplyAutomation(
            GameObject root,
            DimensionAutomationTemplate automation,
            System.Action<string> report)
        {
            if (automation == null)
            {
                automation = new DimensionAutomationTemplate();
            }

            Toggle<Pug.Automation.AffectedByAutomationAuthoring>(
                root,
                automation.AutomationMayActOnIt);
            Toggle<Pug.Automation.AutomatedMineableAuthoring>(root, automation.ADrillCanMineIt);
            Toggle<Pug.Automation.AutomatedPlantableSeedAuthoring>(
                root,
                automation.ASeederCanPlantIt);
            // A CRAFTER NEEDS SLOTS TO CRAFT INTO. Core Keeper counts the machine's crafting slots
            // off its inventory, so a crafter with none is given a timer list of zero length and
            // can never finish anything — and the crafting answer beside it is attached by Unity
            // anyway, because the crafter component requires one, arriving empty and silencing the
            // framework's own "this bench has nothing to craft" warning. The game's own furnace
            // and cooking pot both carry a crafting answer AND an inventory beside the crafter.
            if (automation.AutomationCanCraftAtIt)
            {
                EnsureComponent<Pug.Automation.AutomatedCrafterAuthoring>(root);
                EnsureComponent<CraftingAuthoring>(root);

                bool hadSomewhereToPutThings = HasNamed(root, "InventoryAuthoring");
                EnsureInventory(root);
                SayWhenTicked(
                    !hadSomewhereToPutThings,
                    report,
                    "lets automation craft at it, and a machine crafts into its own slots — with " +
                    "none, the game gives it nothing to work in and it never finishes anything. " +
                    "Slots were filled in. Build it as a Station if you want to choose their size " +
                    "and what goes in them.");
            }
            else
            {
                RemoveComponentIfPresent<Pug.Automation.AutomatedCrafterAuthoring>(root);
            }

            Toggle<Pug.Automation.AutomatedMoverAuthoring>(root, automation.ItMovesThings);

            // The push is its own component and its own question: a belt that shoves the player
            // standing on it is not the same as a belt that carries items, and vanilla has both.
            // Applying it inside the mover path meant a pushing belt that carried nothing never
            // got its push at all.
            ApplyConveyorPush(root, automation, report);

            if (!automation.ItMovesThings)
            {
                RemoveComponentIfPresent<AutomatedMoverSharedAuthoring>(root);
                return;
            }

            if (automation.MovesNothingBecauseAMoveTakesNoTime && report != null)
            {
                report(
                    "moves things with a move time of zero, so no move ever completes. It reads as " +
                    "a conveyor that is switched on and does nothing.");
            }

            // WHICH WAY IT FACES. The game works out a mover's direction from the object's look,
            // and falls back to south for anything that cannot answer — so a belt, an arm and a
            // drill all pointed the same way whatever the player built. Every one of the game's
            // belts, arms and drills carries the answer that turns a variation into a direction.
            EnsureComponent<DirectionBasedOnVariationAuthoring>(root);

            // WHAT IT ACTUALLY CARRIES, which is nothing, and there is no way to say otherwise
            // from here. Core Keeper builds a mover's work from a list of tiles it reaches and
            // where it takes each one — a list the game's own belts and arms have and this
            // framework has no control for. Nothing plausible can be filled in: guessing a reach
            // would be inventing behaviour the author never asked for.
            SayWhenTicked(
                true,
                report,
                "moves things, but which tiles it reaches and where it takes them is not something " +
                "this framework can set yet, and the game builds the whole move out of that list. " +
                "It will run, be powered, face the right way and carry nothing. Use 'things " +
                "standing on it are pushed along' for a working conveyor, and put anything that " +
                "has to be carried into a container beside it.");

            AutomatedMoverSharedAuthoring shared =
                EnsureComponent<AutomatedMoverSharedAuthoring>(root);
            shared.moveTime = automation.MoveSeconds;
            shared.cooldownTime = automation.RestSeconds;
            shared.pickUpDuringMove = automation.PicksUpDuringTheMove;
            shared.allowPickupFromInventories = automation.MayTakeFromInventories;
            shared.splitOnMove = automation.SplitsStacks;
            shared.allowOnlyOneActiveMoverAtATime = automation.OnlyOneAtATime;
            shared.enabledMovers =
                (AutomatedMoverSharedAuthoring.CyclingType)(int)automation.Cycling;
        }

        /// <summary>
        /// The three proximity gates: held item, nearby object, melody. The fourth — a key put
        /// inside — lives on containers, where vanilla keeps it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each gate is one vanilla component doing exactly what its tooltip says; they stack,
        /// which is how the game itself builds "hold the lantern near the singing wall" puzzles.
        /// Every one is added or removed, so unticking in the Studio really closes the gate.
        /// </para>
        /// <para>
        /// IT SHARES TWO OF THOSE COMPONENTS WITH THE REACTS-TO-NEARBY BLOCK, so it is told what
        /// that block claimed. This pass runs second, and its removals used to be unconditional —
        /// so an object that reacts to a nearby lantern, with no gate authored at all, had its
        /// reaction taken straight back off, silently. It now removes only what nothing else
        /// wanted, and says so when the two blocks ask for the same component.
        /// </para>
        /// </remarks>
        public static void ApplyGates(
            GameObject root,
            DimensionGateTemplate gate,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            DimensionReactsToNearbyTemplate reacts = null)
        {
            bool reactsClaimsNearby = reacts != null &&
                reacts.ReactsToANearbyObject && !reacts.WatchesForNothing;
            bool reactsClaimsHeld = reacts != null &&
                reacts.ReactsToAHeldObject && !reacts.WatchesForNothingHeld;

            if (root == null || gate == null || !gate.HasAnyGate)
            {
                if (!reactsClaimsHeld)
                {
                    RemoveComponentIfPresent<ChangeVariationWhenPlayerHoldObjectNearbyAuthoring>(root);
                }

                if (!reactsClaimsNearby)
                {
                    RemoveComponentIfPresent<ChangeVariationWhenObjectNearbyAuthoring>(root);
                }

                return;
            }

            // ---- held item ----
            ObjectID held = string.IsNullOrEmpty(gate.OpensWhenHolding) || resolveObject == null
                ? ObjectID.None
                : resolveObject(gate.OpensWhenHolding);
            if (held != ObjectID.None)
            {
                ChangeVariationWhenPlayerHoldObjectNearbyAuthoring holding =
                    EnsureComponent<ChangeVariationWhenPlayerHoldObjectNearbyAuthoring>(root);
                holding.objectID = held;
                holding.radius = gate.HoldingReach;
                holding.variationToChangeTo = gate.HoldingOpenLook;

                // ALWAYS FALSE, and the author is told. Nothing in the game reads
                // ChangeVariationWhenPlayerHoldObjectNearbyCD.alsoRemoveCollider — a search over
                // the whole of ck-db finds a reader only on the CONTAINING-object variant, in
                // UpdateColliderWhenChangeVariationWhenContainingObjectSystem. The converter
                // copies the field and no system ever looks at it, so writing the author's answer
                // into it produced the same behaviour whichever way it was set. What DOES happen
                // is ResetColliderAfterVariationChangeSystem restoring the hitbox from the
                // object's own prefab every time the look changes, and with one prefab per object
                // that hitbox is the one it already had.
                holding.alsoRemoveCollider = false;

                SayWhenTicked(
                    gate.HoldingOpensTheWay,
                    report,
                    "is set to let players walk through it while it is held open, and it was " +
                    "generated keeping its hitbox. The game only takes a hitbox away for the " +
                    "gate that opens when something is put INSIDE it; for a gate that opens for " +
                    "a held item it reads nothing, and the hitbox is put straight back from the " +
                    "object's own shape. Its look still changes.");

                if (reactsClaimsHeld && report != null)
                {
                    report(
                        "opens for a player holding '" + gate.OpensWhenHolding + "' and also " +
                        "reacts to a player holding '" + reacts.WatchesForHeldObjectId + "'. An " +
                        "object can only watch for one held thing, so the gate is used. Clear one " +
                        "of the two.");
                }
            }
            else if (!reactsClaimsHeld)
            {
                RemoveComponentIfPresent<ChangeVariationWhenPlayerHoldObjectNearbyAuthoring>(root);
                if (!string.IsNullOrEmpty(gate.OpensWhenHolding) && report != null)
                {
                    report(
                        "opens for a player holding '" + gate.OpensWhenHolding + "', which is " +
                        "not a known object, so that gate never opens.");
                }
            }
            else if (!string.IsNullOrEmpty(gate.OpensWhenHolding) && report != null)
            {
                report(
                    "opens for a player holding '" + gate.OpensWhenHolding + "', which is not a " +
                    "known object, so that gate never opens. What it reacts to when held is used " +
                    "instead.");
            }

            // ---- nearby object ----
            ObjectID nearby = string.IsNullOrEmpty(gate.OpensWhenObjectNearby) || resolveObject == null
                ? ObjectID.None
                : resolveObject(gate.OpensWhenObjectNearby);
            if (nearby != ObjectID.None)
            {
                ChangeVariationWhenObjectNearbyAuthoring nearbyGate =
                    EnsureComponent<ChangeVariationWhenObjectNearbyAuthoring>(root);
                nearbyGate.objectID = nearby;
                nearbyGate.objectNearbySpecificVariation = false;
                nearbyGate.radius = gate.NearbyReach;
                nearbyGate.variationToChangeTo = gate.NearbyOpenLook;
                nearbyGate.dontRevertToOriginalVariation = gate.NearbyStaysOpen;

                if (reactsClaimsNearby && report != null)
                {
                    report(
                        "opens when '" + gate.OpensWhenObjectNearby + "' stands nearby and also " +
                        "reacts to '" + reacts.WatchesForObjectId + "' being placed near it. An " +
                        "object can only watch for one nearby thing, so the gate is used. Clear " +
                        "one of the two.");
                }
            }
            else if (!reactsClaimsNearby)
            {
                RemoveComponentIfPresent<ChangeVariationWhenObjectNearbyAuthoring>(root);
                if (!string.IsNullOrEmpty(gate.OpensWhenObjectNearby) && report != null)
                {
                    report(
                        "opens when '" + gate.OpensWhenObjectNearby + "' stands nearby, which " +
                        "is not a known object, so that gate never opens.");
                }
            }
            else if (!string.IsNullOrEmpty(gate.OpensWhenObjectNearby) && report != null)
            {
                report(
                    "opens when '" + gate.OpensWhenObjectNearby + "' stands nearby, which is not " +
                    "a known object, so that gate never opens. What it reacts to nearby is used " +
                    "instead.");
            }

            // ---- melody ----
            // THE MELODY GATE IS WRITTEN BY ApplyMelodyResponse, NOT HERE, and it has to be that
            // way round. Both blocks write the one AffectObjectWhenMelodyPlayedAuthoring, and this
            // one ran first: the melody pass then removed the component whenever its own block was
            // empty, so a door authored to open on a tune never opened and nothing said why. There
            // is one writer now, and it is handed both blocks.
        }

        public static void ApplyMachineRoles(
            GameObject root,
            DimensionMachineRolesTemplate machine,
            System.Action<string> report)
        {
            if (root == null || machine == null)
            {
                return;
            }

            Toggle<DrillAuthoring>(root, machine.IsADrill);
            // The filter is read only while Core Keeper is building the machine's MOVING half, and
            // that component declares nothing it needs, so ticking the filter on a machine that
            // does not move things writes it into a branch the game never enters. The game's own
            // robot arm carries the filter beside the moving answer. Supplied, because the moving
            // block is a component this object can carry, and said so the author knows the object
            // now moves things.
            if (machine.FiltersWhatBeltsCarry)
            {
                EnsureComponent<AutomatedApplyFilterForMoversAuthoring>(root);
                if (!HasNamed(root, "AutomatedMoverSharedAuthoring"))
                {
                    EnsureComponent<AutomatedMoverSharedAuthoring>(root);
                    SayWhenTicked(
                        true,
                        report,
                        "filters what belts carry, and the game only reads a filter on something " +
                        "that carries things in the first place. The moving half was filled in. " +
                        "Set the move timing under Automation, or the arm will run as fast as the " +
                        "game does.");
                }
            }
            else
            {
                RemoveComponentIfPresent<AutomatedApplyFilterForMoversAuthoring>(root);
            }
            Toggle<AnvilAuthoring>(root, machine.IsAnAnvil);
            Toggle<PrioritizedRepairMaterialAuthoring>(root, machine.IsPreferredForRepairs);
            Toggle<CommandMinionWeaponAuthoring>(root, machine.CommandsMinions);
            SayWhenTicked(
                machine.CommandsMinions,
                report,
                "commands minions. Core Keeper reads that off the thing a player is swinging — the " +
                "tome of fire and the other summoning weapons — so on a placed object nobody will " +
                "be commanding anything. Put the answer on the weapon instead.");

            if (machine.IsAnAutomatedMiner && !machine.MinesNothing)
            {
                // Which way the drill faces. Without it the game points every drill south and the
                // bite offset never turns with the machine. Every one of the game's drills carries
                // the answer that turns a variation into a direction.
                EnsureComponent<DirectionBasedOnVariationAuthoring>(root);

                Pug.Automation.AutomatedMinerAuthoring miner =
                    EnsureComponent<Pug.Automation.AutomatedMinerAuthoring>(root);
                miner.damage = machine.BitesFor;
                miner.cooldownTime = machine.SecondsBetweenBites;
                miner.damagePositions =
                    new System.Collections.Generic.List<Unity.Mathematics.int2>();

                Vector2Int[] bite = machine.ChewsTilesAt;
                for (int i = 0; i < bite.Length; i++)
                {
                    miner.damagePositions.Add(new Unity.Mathematics.int2(bite[i].x, bite[i].y));
                }

                // ONLY THE FIRST ONE IS EVER USED. Core Keeper reads a single offset off this list
                // and never looks at the rest, so a drill told to chew four tiles chews one. The
                // whole list is still written, because the game may one day read more of it and
                // because throwing the author's answer away would be worse than saying so.
                SayWhenTicked(
                    bite.Length > 1,
                    report,
                    "chews at " + bite.Length + " tiles, and Core Keeper only ever uses the first " +
                    "one. It will chew at " + bite[0].x + "," + bite[0].y + " and nowhere else. " +
                    "Put more drills down if you want more tiles chewed.");
            }
            else
            {
                RemoveComponentIfPresent<Pug.Automation.AutomatedMinerAuthoring>(root);
            }

            if (report == null)
            {
                return;
            }

            if (machine.MinesNothing)
            {
                report(
                    "mines on its own with no tiles listed to chew, so it runs and never digs. " +
                    "List at least one offset — (1,0) is the tile to its right.");
            }

            if (machine.BitesForNothing)
            {
                report("mines on its own for no damage, so nothing it chews ever breaks.");
            }
        }

        public static void ApplyExtractable(
            GameObject root,
            DimensionExtractableTemplate extractable,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            if (root == null || extractable == null || !extractable.YieldsAnything)
            {
                RemoveComponentIfPresent<ExtractableAuthoring>(root);
                if (extractable != null && extractable.ExtractionTimeWillBeIgnored && report != null)
                {
                    report(
                        "sets an extraction time without listing anything a machine can pull out of " +
                        "it, so machines get nothing from it.");
                }

                return;
            }

            ExtractableAuthoring authored = EnsureComponent<ExtractableAuthoring>(root);
            authored.craftingTimeOverride = extractable.ExtractionSecondsOverride;
            authored.extractedObject =
                new System.Collections.Generic.List<ExtractableAuthoring.ExtractableOutput>();

            DimensionExtractableOutput[] yields = extractable.Yields;
            for (int i = 0; i < yields.Length; i++)
            {
                ObjectID pulled = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(yields[i].ObjectId);
                if (pulled == ObjectID.None)
                {
                    if (report != null)
                    {
                        report(
                            "yields '" + yields[i].ObjectId + "' to a machine, which the game does " +
                            "not have, so that one is left out.");
                    }

                    continue;
                }

                authored.extractedObject.Add(new ExtractableAuthoring.ExtractableOutput
                {
                    objectID = pulled,
                    variation = yields[i].Variation,
                    minMaxRandomAmountOverride = yields[i].AmountRange
                });
            }

            if (authored.extractedObject.Count == 0)
            {
                RemoveComponentIfPresent<ExtractableAuthoring>(root);
            }
        }

        /// <summary>
        /// Makes an object switch on and off with electricity, and change while it runs.
        /// </summary>
        public static void ApplyPoweredMachine(
            GameObject root,
            DimensionPoweredMachineTemplate machine,
            System.Action<string> report)
        {
            if (root == null || machine == null || !machine.ReactsToBeingPowered)
            {
                RemoveComponentIfPresent<ActivatedByElectricityStateAuthoring>(root);
                return;
            }

            ActivatedByElectricityStateAuthoring powered =
                EnsureComponent<ActivatedByElectricityStateAuthoring>(root);
            powered.activationTime = machine.SwitchOnSeconds;
            powered.deactivationTime = machine.SwitchOffSeconds;
            powered.changeVariationOnActivate = machine.ChangesLookWhileRunning;
            powered.variationToChangeTo = machine.RunningVariation;
            powered.changeColliderToTriggerWhenActivated = machine.StopsBlockingWhileRunning;
            powered.triggerBelongsTo = new Unity.Physics.Authoring.PhysicsCategoryTags
            {
                Value = (uint)machine.TriggerBelongsToLayers
            };
            powered.triggerCollidesWith = new Unity.Physics.Authoring.PhysicsCategoryTags
            {
                Value = (uint)machine.TriggerNoticesLayers
            };

            if (report == null)
            {
                return;
            }

            if (machine.RunningLookWillBeIgnored)
            {
                report(
                    "names a look to wear while running without being told to change look, so it " +
                    "looks the same powered or not.");
            }

            if (machine.TriggerLayersWillBeIgnored)
            {
                report(
                    "sets trigger layers on a machine whose collider never changes, so those " +
                    "layers are never read.");
            }
        }
    }
}
