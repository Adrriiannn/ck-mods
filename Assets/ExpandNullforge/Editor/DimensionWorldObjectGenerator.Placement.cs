using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Where a world object may be put, what kind it is, and the light it casts.
    /// </summary>
    internal static partial class DimensionWorldObjectGenerator
    {
        /// <summary>Whether the author called this world object a door.</summary>
        private static bool ItIsADoor(DimensionWorldObjectAsset worldObject)
        {
            return worldObject != null && worldObject.Kind == DimensionWorldObjectKind.Door;
        }

        /// <remarks>
        /// All three are removed first, so changing a door into a decoration actually stops it
        /// being a door — the marker the pathfinder reads goes with it — rather than leaving it
        /// carrying the old kind with nothing asking.
        /// </remarks>
        private static void ApplyKind(
            GameObject root,
            DimensionWorldObjectAsset worldObject,
            DimensionNamingContext naming,
            DimensionWorldObjectGenerationReport report)
        {
            RemoveComponentIfPresent<DoorAuthoring>(root);
            RemoveComponentIfPresent<BedAuthoring>(root);
            RemoveComponentIfPresent<TrophyAuthoring>(root);

            switch (worldObject.Kind)
            {
                case DimensionWorldObjectKind.Door:
                {
                    // FALSE, AND THAT IS THE FIX FOR A CRASH THIS FRAMEWORK CREATED. With it true,
                    // DoorConverter adds ColliderVariationCD, and ColliderVariationSystem's query
                    // is ObjectDataCD + PhysicsCollider + ColliderVariationCD. A generated door
                    // used to carry no PhysicsCollider, so it never matched; now that every placed
                    // object has one, it matches.
                    //
                    // IT IS NO LONGER A CRASH. Every generated object now says its look is not
                    // part of its identity (DimensionQueryCompanions.ItsLookIsNotPartOfItsIdentity)
                    // so the lookup returns this door's own prefab rather than Entity.Null. What
                    // the system would then copy is the collider the door already has, because
                    // this framework writes one prefab per object. Staying out of the query says
                    // the same thing without spending a job on it, and it keeps the door's active
                    // variation from being tracked as if a swap were happening.
                    EnsureComponent<DoorAuthoring>(root).changesColliderByVariation = false;

                    report.Warnings.Add(
                        "'" + worldObject.DisplayName + "' is a door, and it keeps one body and " +
                        "one look whether it is open or shut, so a player cannot walk through the " +
                        "doorway and the door does not appear to move. Core Keeper builds an open " +
                        "door as a second object with its own picture and its own shape, and this " +
                        "framework writes one object per door. Its shut body lies " +
                        (worldObject.Rules.DoorwayRunsAlongTheWall
                            ? "along the wall, blocking a doorway that runs north to south."
                            : "across the corridor, blocking a doorway that runs east to west. " +
                              "If the wall it sits in runs north to south, tick the doorway " +
                              "answer on its rules, or the hitbox lies at right angles to the " +
                              "door and a player is stopped beside the doorway instead of in it."));

                    // A DOOR WITH ONLY THIS NEVER OPENS. DoorConverter produces a marker the
                    // pathfinder reads and nothing that swings. Opening is
                    // TriggerSetVariationSystem, and the component that feeds it is added by the
                    // sweep at the end of Configure, which is the only point where the door's body
                    // exists and can be checked. Half of the fix belongs here, though: the number
                    // the system toggles the look TO.
                    //
                    // It sets the object's variation to this number when used. Left at zero, a door
                    // that opened would toggle to the look it already had. One is the second look —
                    // closed is the first — which is how the game's two-look doors are built.
                    ObjectAuthoring identity = root.GetComponent<ObjectAuthoring>();
                    if (identity != null && identity.variationToToggleTo == 0)
                    {
                        identity.variationToToggleTo = 1;
                    }

                    break;
                }

                case DimensionWorldObjectKind.Bed:
                    // Lying down needs an occupiable slot beside this, which the sweep at the end
                    // of Configure checks — it cannot be checked here, because the pass that adds
                    // the slot runs after this one.
                    EnsureComponent<BedAuthoring>(root);
                    break;

                case DimensionWorldObjectKind.Trophy:
                {
                    TrophyAuthoring trophy = EnsureComponent<TrophyAuthoring>(root);
                    ObjectID enemy = ResolveObject(worldObject.SummonsEnemyId);

                    // Written every time, the None included. TrophyConverter writes TrophyCD
                    // whatever the id is, so for one of the mod's own creatures the None is the
                    // placeholder the link hydration overwrites at load — and writing it also
                    // stops a creature changed to a typo from quietly staying the old one.
                    trophy.enemyToSpawnFromSpawnerPlatform = enemy;
                    if (enemy == ObjectID.None &&
                        !IsDeferred(worldObject.SummonsEnemyId) &&
                        !string.IsNullOrEmpty(worldObject.SummonsEnemyId))
                    {
                        report.Warnings.Add(
                            "'" + worldObject.DisplayName + "' is a trophy for '" +
                            worldObject.SummonsEnemyId + "', which is neither one of this mod's " +
                            "creatures nor one the game has. It will summon nothing.");
                    }

                    break;
                }
            }
        }

        private static void ApplyPlacement(
            GameObject root,
            DimensionWorldObjectAsset worldObject,
            System.Func<string, int> resolveTileset,
            DimensionWorldObjectGenerationReport report,
            DimensionNamingContext naming)
        {
            PlaceableObjectAuthoring placeable = EnsureComponent<PlaceableObjectAuthoring>(root);
            DimensionObjectSpine.ApplyWorldRoles(
                root,
                worldObject.WorldRoles,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                IsDeferred,
                QualifyReference);

            DimensionObjectSpine.ApplyEventTerminal(
                root,
                worldObject.EventTerminal,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyFinalTouches(
                root,
                worldObject.FinalTouches,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyBeamAndAmbience(
                root,
                worldObject.BeamAndAmbience,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyHidingAndHatching(
                root,
                worldObject.HidingAndHatching,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyManaAndAura(
                root,
                worldObject.ManaAndAura,
                ResolveObject,
                resolveTileset,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplySpawnerAndOrb(
                root,
                worldObject.SpawnerAndOrb,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyChainReaction(
                root,
                worldObject.ChainReaction,
                ResolveObject,
                resolveTileset,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                IsDeferred);

            DimensionObjectSpine.ApplyMachineRoles(
                root,
                worldObject.MachineRoles,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyTerrainEffects(
                root,
                worldObject.TerrainEffects,
                resolveTileset,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyLooksAtItsSurroundings(
                root,
                worldObject.AdaptsToSurroundings,
                resolveTileset,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyNest(
                root,
                worldObject.Nest,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyTrader(
                root,
                worldObject.Trader,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                IsDeferred);

            DimensionObjectSpine.ApplyObjectRoles(
                root,
                worldObject.Roles,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyReactsToNearby(
                root,
                worldObject.ReactsToNearby,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplySummoningCircle(
                root,
                worldObject.SummoningCircle,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                delegate(string ownId)
                {
                    // The mod's own creatures have no ids until runtime; their qualified name
                    // is what hydration resolves.
                    return naming.QualifyGenerated(ownId);
                });

            // The reacts-to-nearby block above writes two of the same components this one does, so
            // this one is told what it claimed. Without that, an object that only reacts to
            // something nearby had its reaction removed here, one call later, in silence.
            DimensionObjectSpine.ApplyGates(
                root,
                worldObject.Gate,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                worldObject.ReactsToNearby);

            DimensionObjectSpine.ApplyKeepsItsFloor(
                root,
                worldObject.KeepsItsFloor,
                resolveTileset,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyExtractable(
                root,
                worldObject.Extractable,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            DimensionObjectSpine.ApplyPoweredMachine(
                root,
                worldObject.PoweredMachine,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });

            // The gate block goes in with the melody block: they write the same component, and
            // handing this pass both is what keeps a door that opens to a tune working. It runs
            // after ApplyGates for the same reason it always did — the gate's other two openings
            // are separate components.
            DimensionObjectSpine.ApplyMelodyResponse(
                root,
                worldObject.MelodyResponse,
                ResolveObject,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                },
                IsDeferred,
                worldObject.Gate);

            DimensionObjectSpine.ApplyPlacementRules(
                root,
                worldObject.PlacementRules,
                delegate(string message)
                {
                    report.Warnings.Add("'" + worldObject.DisplayName + "' " + message);
                });
            placeable.prefabTileSize = worldObject.TileSize;

            // NOTHING EVER WROTE THIS BEFORE. The body calculation has carried a corner-offset
            // term since the pass that measured PlanterBoxEntity, and the only writer in the tree
            // set it to zero, so the term could not do anything on any path that existed. It is
            // the author's answer now, and it moves the hitbox and the placement together.
            placeable.prefabCornerOffset = worldObject.StandsOnTile;
            placeable.canBePlacedOnAnyWalkableTile = true;
            placeable.canBePlacedOnWater = worldObject.CanBePlacedOnWater;

            if (worldObject.SurfacePriority != 0)
            {
                EnsureComponent<SurfacePriorityAuthoring>(root).Value = worldObject.SurfacePriority;
            }
            else
            {
                RemoveComponentIfPresent<SurfacePriorityAuthoring>(root);
            }
        }

        /// <summary>
        /// The three separate lighting mechanisms that live on the entity, written independently.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A torch carries <c>ActAsLightSourceWhenHeldInHand</c> and <c>TableItemLightSource</c> and
        /// NOT <c>GlowLight</c>. Collapsing them into one answer would produce lamps that glow and
        /// light nothing.
        /// </para>
        /// <para>
        /// THE FOURTH ONE IS NOT HERE, AND CANNOT BE. The light a placed object throws on the floor
        /// around it has no component on the entity at all — Core Keeper keeps it as a subtree of
        /// nodes in the graphical prefab — so it is written by
        /// <c>DimensionInteractionVisualUtility</c>, from the answers on the asset's own light
        /// block. Whoever comes looking for it here should look there.
        /// </para>
        /// </remarks>
        private static void ApplyLight(GameObject root, DimensionWorldObjectAsset worldObject)
        {
            if (worldObject.LightsTheRoomWhenPlaced)
            {
                EnsureComponent<TableItemLightSourceAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<TableItemLightSourceAuthoring>(root);
            }

            if (worldObject.LightsTheRoomWhenHeld)
            {
                ActAsLightSourceWhenHeldInHandAuthoring held =
                    EnsureComponent<ActAsLightSourceWhenHeldInHandAuthoring>(root);
                held.color = worldObject.HeldLightColor;
                held.range = worldObject.HeldLightRange;
            }
            else
            {
                RemoveComponentIfPresent<ActAsLightSourceWhenHeldInHandAuthoring>(root);
            }

            if (worldObject.ObjectItselfGlows)
            {
                GlowLightAuthoring glow = EnsureComponent<GlowLightAuthoring>(root);
                glow.color = worldObject.GlowColor;
                glow.intensity = worldObject.GlowIntensity;
            }
            else
            {
                RemoveComponentIfPresent<GlowLightAuthoring>(root);
            }
        }
    }
}
