using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// What a creature does when nothing is fighting it: roam, patrol, chase, idle.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        private static void ApplyIdleEmotes(
            GameObject root,
            DimensionCreatureLifecycleTemplate lifecycle)
        {
            if (!lifecycle.HasIdleEmotes)
            {
                RemoveComponentIfPresent<IdleEmoteStateAuthoring>(root);
                return;
            }

            IdleEmoteStateAuthoring emotes = EnsureComponent<IdleEmoteStateAuthoring>(root);
            emotes.minCooldown = lifecycle.MinimumIdleGap;
            emotes.maxCooldown = lifecycle.MaximumIdleGap;
            emotes.emoteAnimations =
                new System.Collections.Generic.List<IdleEmoteStateAuthoring.EmoteAnimation>();

            DimensionIdleEmote[] authored = lifecycle.IdleEmotes;
            for (int i = 0; i < authored.Length; i++)
            {
                emotes.emoteAnimations.Add(new IdleEmoteStateAuthoring.EmoteAnimation
                {
                    animation = authored[i].Animation,
                    duration = authored[i].Seconds,
                    preIdleMinDuration = authored[i].MinimumWait,
                    preIdleMaxDuration = authored[i].MaximumWait,
                    mustBeOnWalkableGround = authored[i].OnlyOnWalkableGround
                });
            }
        }

        /// <summary>
        /// What swinging or firing this sounds like.
        /// </summary>
        /// <remarks>
        /// Removed when nothing is chosen rather than written as five zeroes, so a weapon stripped
        /// of its sounds falls back to the game's own rather than to silence.
        /// </remarks>
        /// <summary>
        /// Writes how a creature closes on its target onto an existing <c>ChaseStateAuthoring</c>.
        /// </summary>
        /// <remarks>
        /// Deliberately does NOT add the component. Chasing is only meaningful on something that
        /// already chases, and the creature generator decides that from the movement questions. A
        /// spine method that added it would give every stationary object a pursuit it never runs.
        /// </remarks>
        public static void ApplyPursuit(GameObject root, DimensionPursuitTemplate pursuit)
        {
            if (root == null || pursuit == null)
            {
                return;
            }

            ChaseStateAuthoring chase = root.GetComponent<ChaseStateAuthoring>();
            if (chase == null)
            {
                return;
            }

            chase.minDistanceToKeep = pursuit.KeepsAtLeastThisFarAway;
            chase.maxDistanceToKeep = pursuit.AndAtMostThisFarAway;
            chase.distanceToStartSideStepping = pursuit.StartsSideSteppingWithin;
            chase.distanceToKeepNoiseDisabled = pursuit.KeepsQuietAboutItsDistance;

            chase.neverStopChasing = pursuit.NeverGivesUp;
            chase.skipVisibilityCheck = pursuit.ChasesWhatItCannotSee;
            chase.ignoreLowColliders = pursuit.LowObstaclesDoNotStopIt;

            chase.needPathToChase = pursuit.NeedsAPathToChase;
            chase.preferPathFind = pursuit.PrefersPathfinding;
            chase.obstacleAvoidDistance = pursuit.LooksAheadToAvoidObstacles;

            chase.preChaseDuration = pursuit.PauseBeforeChasing;
            chase.endChaseDuration = pursuit.KeepsGoingAfterLosingIt;
            chase.idleDuration = pursuit.IdlesMidChaseFor;
            chase.idleCooldown = pursuit.BetweenMidChaseIdles;

            chase.disabled = pursuit.StartsSwitchedOff;

            // belongsToShape is the physics shape the pathfinding entity is built against
            // (PathFindingConversion.CreatePathfindingEntity reads it). There is exactly one
            // sensible value — the creature's own shape — so it is wired rather than asked about.
            // Left null when the creature has no shape, which is what the game already handles.
            chase.belongsToShape = root.GetComponent<Unity.Physics.Authoring.PhysicsShapeAuthoring>();

            // chaseHeldObjects is left alone on purpose: zero vanilla prefabs set it, so there is
            // nothing to copy and no way to know what the game does with a value nobody authored.
            // belongsToShape is a scene reference the creature does not have, and disabled is what
            // the generator's own "does it chase at all" question already decides.
            if (chase.chaseHeldObjects == null)
            {
                chase.chaseHeldObjects = new System.Collections.Generic.List<ObjectID>();
            }
        }

        /// <summary>
        /// Gives a creature a route to walk rather than a patch to mill around in.
        /// </summary>
        public static void ApplyPatrolPath(
            GameObject root,
            DimensionPatrolPathTemplate patrol,
            System.Func<string, int> resolveTileset,
            System.Action<string> report)
        {
            if (root == null || patrol == null || !patrol.WalksARoute || patrol.RouteHasNoPoints)
            {
                RemoveComponentIfPresent<RoamingPathAuthoring>(root);
                if (patrol != null && patrol.RouteHasNoPoints && report != null)
                {
                    report(
                        "walks a route with no turning points on it, so there is no route to walk. " +
                        "Give it at least one.");
                }

                return;
            }

            RoamingPathAuthoring path = EnsureComponent<RoamingPathAuthoring>(root);
            path.pathType = (RoamingPathType)(int)patrol.Shape;
            path.regionRadius = patrol.ReachesOut;
            path.pointCount = patrol.TurningPoints;
            path.segmentation = patrol.Smoothness;
            path.distanceBetweenPoints = patrol.GapBetweenPoints;
            path.pathLengthMultiplier = patrol.RouteLengthMultiplier;
            path.zigzagAmount = patrol.Weave;
            path.distanceDeviation = patrol.DistanceVariation;
            path.curveSmoothness = patrol.CornerRoundness;
            path.angleDeviation = patrol.TurnVariation;
            path.pointsBetweenAngleDeviationChanges = patrol.PointsBetweenTurns;
            path.minAngleToBiomeMidpoint = patrol.MinAngleToBiomeCentre;
            path.maxAngleToBiomeMidpoint = patrol.MaxAngleToBiomeCentre;
            path.roamAroundPlayerIfInSubBiome = patrol.FollowsThePlayerInSubBiomes;
            path.drawDebugLines = patrol.ShowTheRouteWhileBuilding;

            Biome biome;
            if (!string.IsNullOrEmpty(patrol.StaysInBiome) &&
                System.Enum.TryParse(patrol.StaysInBiome, false, out biome))
            {
                path.forceBiome = new Pug.UnityExtensions.OptionalValue<Biome>(biome);
            }
            else
            {
                path.forceBiome = default(Pug.UnityExtensions.OptionalValue<Biome>);
                if (!string.IsNullOrEmpty(patrol.StaysInBiome) && report != null)
                {
                    report(
                        "keeps its route inside biome '" + patrol.StaysInBiome + "', which the game " +
                        "does not have, so it roams anywhere.");
                }
            }

            // A null resolver is normal here — the creature generator has no mod-tileset index,
            // and a sub-biome check is nearly always against one of the game's own grounds. Fall
            // back to the vanilla name so the field still works without one.
            int subBiome = -1;
            if (!string.IsNullOrEmpty(patrol.SubBiomeTilesetId))
            {
                if (resolveTileset != null)
                {
                    subBiome = resolveTileset(patrol.SubBiomeTilesetId);
                }

                PugTilemap.Tileset named;
                if (subBiome < 0 &&
                    System.Enum.TryParse(patrol.SubBiomeTilesetId, false, out named))
                {
                    subBiome = (int)named;
                }
            }

            if (subBiome >= 0)
            {
                path.playerCheckSubBiomeTileset = (PugTilemap.Tileset)subBiome;
            }
            else if (!string.IsNullOrEmpty(patrol.SubBiomeTilesetId) && report != null)
            {
                report(
                    "watches for sub-biome ground '" + patrol.SubBiomeTilesetId + "', which is not " +
                    "a tileset, so it never switches to following the player.");
            }

            if (patrol.BiomeRuleWillBeIgnored && report != null)
            {
                report(
                    "names a biome to keep its route inside, but its route shape does not stay in a " +
                    "biome. Choose the biome-bound shape for that to matter.");
            }
        }

        public static void ApplyCreatureHabits(
            GameObject root,
            DimensionCreatureHabitsTemplate habits,
            System.Action<string> report)
        {
            if (root == null || habits == null)
            {
                return;
            }

            if (habits.NoticesAPlayerWithin > 0f)
            {
                EnsureComponent<IdleWhenNearbyPlayerStateAuthoring>(root).distanceToStartIdle =
                    habits.NoticesAPlayerWithin;
            }
            else
            {
                RemoveComponentIfPresent<IdleWhenNearbyPlayerStateAuthoring>(root);
            }

            if (habits.PausesInCombatBeyond > 0f)
            {
                IdleInCombatStateAuthoring pause =
                    EnsureComponent<IdleInCombatStateAuthoring>(root);

                // The game stores this SQUARED, because it compares against a squared distance to
                // avoid a square root every frame. An author types tiles; this is where that becomes
                // the number the game actually reads.
                pause.sqrDistanceToLeaveCombat =
                    habits.PausesInCombatBeyond * habits.PausesInCombatBeyond;
                pause.checkDistanceToPlayerFromSpawnPointInsteadOfSelf = habits.MeasuresFromItsNest;
            }
            else
            {
                RemoveComponentIfPresent<IdleInCombatStateAuthoring>(root);
            }

            if (habits.TauntsDuringAFight && !habits.TauntsWithNoAnimations)
            {
                CombatEmoteStateAuthoring taunt = EnsureComponent<CombatEmoteStateAuthoring>(root);
                taunt.emoteInstantlyChance = habits.TauntsImmediatelyChance;
                taunt.minCooldown = habits.MinBetweenTaunts;
                taunt.maxCooldown = habits.MaxBetweenTaunts;
                taunt.emoteAnimations =
                    new System.Collections.Generic.List<CombatEmoteStateAuthoring.CombatEmoteAnimation>();

                DimensionTaunt[] list = habits.Taunts;
                for (int i = 0; i < list.Length; i++)
                {
                    taunt.emoteAnimations.Add(new CombatEmoteStateAuthoring.CombatEmoteAnimation
                    {
                        animation = list[i].Animation,
                        duration = list[i].Seconds,
                        preCombatMinDuration = list[i].MinBeforeCombat,
                        preCombatMaxDuration = list[i].MaxBeforeCombat
                    });
                }
            }
            else
            {
                RemoveComponentIfPresent<CombatEmoteStateAuthoring>(root);
                if (habits.TauntsWithNoAnimations && report != null)
                {
                    report("taunts during a fight with no taunt animations listed, so it taunts silently.");
                }
            }

            if (habits.StaysAngryFor > 0f)
            {
                EnsureComponent<OverrideLeaveCombatTimeAuthoring>(root).time = habits.StaysAngryFor;
            }
            else
            {
                RemoveComponentIfPresent<OverrideLeaveCombatTimeAuthoring>(root);
            }

            Toggle<HasSpawnPointAuthoring>(root, habits.KeepsANest);
            Toggle<IsFlyingAuthoring>(root, habits.Flies);
            Toggle<CanClaimBedAuthoring>(root, habits.CanClaimABed);
            Toggle<CattleAuthoring>(root, habits.IsLivestock);

            // SOMEWHERE TO KEEP THE NAME THE TENDING WINDOW ASKS FOR. Everything in the game that
            // gives an animal a name uses `ecb.SetComponent<NameCD>` — the deserializer at
            // `ck-db\Pug.Other\DeserializeComponentsSystem.cs:785`, the cage drop and the aux-data
            // copy at `EntityUtility.cs:160` and `:524` — and SetComponent only writes a component
            // that is already there. `NameConverter` is the one thing that puts it there, and it
            // runs off `NameAuthoring`. Until this line, a generated animal opened a tending window
            // offering to name it and had nowhere to keep the name: `Cattle.GetName` answered null
            // for ever and the floating tag stayed hidden. It rides with being livestock rather
            // than being its own tickbox because the window is the only thing that asks.
            //
            // ONE WRITER, deliberately. The other place in this file that writes NameAuthoring is
            // ApplyObjectRoles, which the creature generator never calls, so nothing here can
            // overwrite what the other one decided for a world object.
            Toggle<NameAuthoring>(root, habits.IsLivestock);
            Toggle<MealsEatenAuthoring>(root, habits.RemembersItsMeals);
            Toggle<PutTargetInCombatOnDealingDamageAuthoring>(
                root,
                habits.DraggingWhatItHurtsIntoTheFight);

            if (habits.GuardsItsNest)
            {
                EnsureComponent<ForceInCombatIfPlayerNearbySpawnPointAuthoring>(root)
                    .distanceToStayInCombat = habits.GuardsWithin;

                // A nest to guard. The game stamps a creature with where its nest IS only when the
                // creature says it keeps one, and the guarding pass then looks for that stamp — so
                // "guards its nest" on its own was two boxes that had to be ticked together and
                // nothing said so. Five of the six creatures in the game that guard a nest keep
                // one as well; this makes it six.
                if (!HasNamed(root, "HasSpawnPointAuthoring"))
                {
                    EnsureComponent<HasSpawnPointAuthoring>(root);
                    SayWhenTicked(
                        true,
                        report,
                        "guards its nest without keeping one, and the game only knows where a " +
                        "nest is for a creature that keeps one. It was generated keeping a nest " +
                        "where it spawns, which is what the game's own nest guards do.");
                }
            }
            else
            {
                RemoveComponentIfPresent<ForceInCombatIfPlayerNearbySpawnPointAuthoring>(root);
            }

            if (habits.GetsFullAt > 0)
            {
                EnsureComponent<FullnessAuthoring>(root).maxFullness = habits.GetsFullAt;
            }
            else
            {
                RemoveComponentIfPresent<FullnessAuthoring>(root);
            }

            if (habits.TakesMoveOrders)
            {
                MoveToPositionFromCommandStateAuthoring orders =
                    EnsureComponent<MoveToPositionFromCommandStateAuthoring>(root);
                orders.belongsToShape =
                    root.GetComponent<Unity.Physics.Authoring.PhysicsShapeAuthoring>();
            }
            else
            {
                RemoveComponentIfPresent<MoveToPositionFromCommandStateAuthoring>(root);
            }

            if (!string.IsNullOrEmpty(habits.UnlocksAchievement))
            {
                AchievementID unlocked;
                if (System.Enum.TryParse(habits.UnlocksAchievement, false, out unlocked))
                {
                    EnsureComponent<TriggerAchievementOnDeathAuthoring>(root).achievement = unlocked;
                }
                else
                {
                    RemoveComponentIfPresent<TriggerAchievementOnDeathAuthoring>(root);
                    if (report != null)
                    {
                        report(
                            "unlocks achievement '" + habits.UnlocksAchievement + "' when killed, " +
                            "which the game does not have, so nothing is unlocked.");
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<TriggerAchievementOnDeathAuthoring>(root);
            }

            if (habits.GuardsWithoutAskingForANest && report != null)
            {
                report(
                    "guards its nest, so it has been given one to guard — guarding is measured from " +
                    "where it spawned.");
            }
        }
    }
}
