using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    internal sealed class DimensionProjectileGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        public bool HasProblems
        {
            get { return Errors.Count > 0 || Warnings.Count > 0; }
        }

        public string Summarize()
        {
            return "Projectiles: " + Created.Count + " created, " + Updated.Count + " updated, " +
                Skipped.Count + " skipped.";
        }
    }

    /// <summary>
    /// Turns a handful of questions about a shot into the object Core Keeper fires.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The component list is taken from vanilla rather than invented: every projectile in the game
    /// carries the same thirteen, and the interesting one is only the first. The rest are what make
    /// it behave like a projectile rather than like a dropped item —
    /// <c>DontDropSelfAuthoring</c> so it does not fall on the floor when it dies,
    /// <c>DontSerializeAuthoring</c> so it is not saved into the world,
    /// <c>CantBeAttackedAuthoring</c> so nothing targets it, and
    /// <c>DestroyTimerAuthoring</c> so a shot that hits nothing eventually stops existing.
    /// </para>
    /// <para>
    /// Leaving any of those off is not a small omission. Without the timer, every missed shot in the
    /// mod stays in the world forever; without the serialize opt-out they are saved into the world
    /// file and come back on load.
    /// </para>
    /// </remarks>
    internal static class DimensionProjectileGenerator
    {
        public const string FolderName = "Projectiles";

        public static DimensionProjectileGenerationReport Generate(
            IEnumerable<DimensionProjectileAsset> projectiles,
            string outputFolder,
            DimensionNamingContext naming,
            IEnumerable<DimensionTilesetAsset> tilesets = null)
        {
            DimensionProjectileGenerationReport report = new DimensionProjectileGenerationReport();
            if (projectiles == null)
            {
                return report;
            }

            if (string.IsNullOrEmpty(outputFolder))
            {
                report.Errors.Add("No output folder was resolved, so no projectiles were generated.");
                return report;
            }

            DimensionAssetFolders.Ensure(outputFolder);
            binder = new DimensionObjectBinder(naming);

            // Artillery can scatter a mod's own tiles as it falls, so the lookup is built once here.
            System.Func<string, int> resolveTileset = BuildTilesetResolver(tilesets);

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (DimensionProjectileAsset projectile in projectiles)
                {
                    GenerateOne(projectile, outputFolder, naming, report, resolveTileset);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return report;
        }

        private static void GenerateOne(
            DimensionProjectileAsset projectile,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionProjectileGenerationReport report,
            System.Func<string, int> resolveTileset)
        {
            if (projectile == null || !projectile.Enabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(projectile.ProjectileId))
            {
                report.Skipped.Add("A projectile with no id was skipped.");
                return;
            }

            if (projectile.NeverGoesAnywhere)
            {
                report.Warnings.Add(
                    "'" + projectile.DisplayName + "' has no speed, so it appears where it was " +
                    "fired from and expires without travelling.");
            }

            if (projectile.CannotHitAnything)
            {
                report.Warnings.Add(
                    "'" + projectile.DisplayName + "' has a hit radius of zero, so it passes " +
                    "through everything without hitting it. Most of the game uses " +
                    DimensionProjectileAsset.OrdinaryHitRadius + ".");
            }

            if (projectile.ShattersIntoNothing)
            {
                report.Warnings.Add(
                    "'" + projectile.DisplayName + "' is set to break into " + projectile.Shards +
                    " pieces without saying what of, so it breaks into nothing.");
            }

            if (projectile.DamagesTerrainOverNoArea)
            {
                report.Warnings.Add(
                    "'" + projectile.DisplayName + "' damages terrain over an area of zero, so the " +
                    "terrain damage can never land.");
            }

            string prefabPath =
                outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(projectile.ProjectileId, "Projectile") + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(projectile.ProjectileId);

            bool written = false;
            try
            {
                Configure(root, projectile, naming, report, resolveTileset);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                written = true;
                if (updating)
                {
                    report.Updated.Add(prefabPath);
                }
                else
                {
                    report.Created.Add(prefabPath);
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    projectile.ProjectileId + " failed to generate: " + exception.Message +
                    "\n" + exception.StackTrace);
            }
            finally
            {
                if (updating)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else if (written)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void Configure(
            GameObject root,
            DimensionProjectileAsset projectile,
            DimensionNamingContext naming,
            DimensionProjectileGenerationReport report,
            System.Func<string, int> resolveTileset)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.objectName = naming.QualifyGenerated(projectile.ProjectileId);
            obj.objectType = ObjectType.NonObtainable;
            obj.initialAmount = 1;

            if (projectile.ArcsLikeArtillery)
            {
                ConfigureArtillery(root, projectile, resolveTileset, report);
            }
            else
            {
                RemoveComponentIfPresent<MortarProjectileAuthoring>(root);
                ConfigureStraightShot(root, projectile, resolveTileset, report);

                // Only an arcing shell carries the component that leaves something behind, so on a
                // straight shot that name reaches nothing at all — including when it is one of the
                // mod's own, where every other check reads it as perfectly good.
                if (!string.IsNullOrEmpty(projectile.LeavesBehindObjectId))
                {
                    report.Warnings.Add(
                        "'" + projectile.DisplayName + "' leaves '" +
                        projectile.LeavesBehindObjectId + "' where it lands, but it flies straight " +
                        "rather than arcing, and only an arcing shot leaves anything. Set it to " +
                        "arc, or clear that name.");
                }
            }

            MovementSpeedAuthoring movement = EnsureComponent<MovementSpeedAuthoring>(root);
            movement.speed = projectile.Speed;

            // Everything below is what makes it a projectile rather than a thing lying on the floor.
            DestroyTimerAuthoring timer = EnsureComponent<DestroyTimerAuthoring>(root);
            timer.lifetime = new Pug.UnityExtensions.PlatformDependentValue<float>(
                projectile.LifetimeSeconds);

            EnsureComponent<DontDropSelfAuthoring>(root);
            EnsureComponent<DontSerializeAuthoring>(root);
            EnsureComponent<CantBeAttackedAuthoring>(root);
            EnsureComponent<FactionAuthoring>(root);
            EnsureComponent<SupportsConditionsAuthoring>(root);
            EnsureComponent<BehaviourTagsAuthoring>(root);

            // THE THREE THAT DECIDE WHETHER IT MOVES AT ALL, and none of them was here.
            //
            // Being sent to players: both of the game's projectile passes open their query with
            // GhostInstance, which only a ghost component produces. Without it a modded arrow
            // appears at the muzzle, stays there, and is deleted by its own timer.
            //
            // Health: ProjectileSystem indexes the projectile's health without checking for it
            // first, and every one of the game's own projectile prefabs carries one. The arcing
            // shell needs it in its work query outright.
            //
            // Animation: the arcing shell's whole flight — rising, falling, landing — is written
            // through the animation buffers, so an arcing shot without them never comes down.
            //
            // AND IT IS FIRED BY SOMEBODY, which is the second half of being sent to players. The
            // game's spawn path for a projectile ends by setting the owner onto it without asking
            // whether it has one — RangeWeaponSlot for a weapon, EntityUtility for a creature's
            // shot — so a projectile that says it has no owner is a set on a component that is not
            // there, and the whole command buffer that spawn was in is lost. All 80 vanilla
            // prefabs that carry an owner are projectiles, bombs, trails and predicted explosions.
            DimensionQueryCompanions.IsSentToPlayers(root, true);

            // THE GAME REFUSES "only lands where it can see" ON ANYTHING BUT A PREDICTED SHOT, and
            // says so at conversion: MortarProjectileConverter logs an error when that answer is on
            // and the object's supported ghost modes are not Predicted. A fresh ghost supports both
            // modes, so ticking the control printed that error every generate and left the
            // visibility test reading tiles the server keeps no history of. Written on both
            // branches, so unticking it — or turning the shell back into a straight shot — takes
            // the narrowing back rather than leaving the last generate's answer on the prefab.
            // It runs after the ghost is on the object, because that is the component it writes.
            DimensionQueryCompanions.ItIsWorkedOutOnEveryClient(
                root,
                projectile.ArcsLikeArtillery && projectile.OnlyLandsWhereItCanSee);

            EnsureComponent<AnimationAuthoring>(root);

            HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
            if (health.maxHealth <= 0)
            {
                health.dontCalculateHealthFromLevel = true;
                health.maxHealth = 1;
                health.startHealth = 1;
                health.maxHealthMultiplier = 1f;
            }

            DimensionObjectSpine.ApplyAttackSounds(
                root,
                projectile.Sounds,
                delegate(string message)
                {
                    report.Warnings.Add("'" + projectile.DisplayName + "' " + message);
                });
        }

        private static void ConfigureStraightShot(
            GameObject root,
            DimensionProjectileAsset projectile,
            System.Func<string, int> resolveTileset,
            DimensionProjectileGenerationReport report)
        {
            ProjectileAuthoring shot = EnsureComponent<ProjectileAuthoring>(root);
            shot.damageRadius = projectile.HitRadius;
            shot.damageRadiusClient = projectile.HitRadius;
            shot.tileDamageRadius = projectile.TerrainHitRadius;
            shot.piercesEnemies = projectile.GoesThroughEnemies;
            shot.damagesTiles = projectile.DamagesTerrain;
            shot.dontDestroyOnCollision = projectile.SurvivesCollision;
            shot.isDamageable = projectile.CanBeShotDown;
            shot.zigZag = projectile.Weaves;
            shot.ExplodeOnEnemyCollision = projectile.ExplodesOnEnemies;
            shot.collideWithNonWalkableTiles = projectile.StopsOnUnwalkableTiles;
            shot.treatDodgeAsHit = projectile.DodgingDoesNotSaveYou;
            shot.maxBounceCount = projectile.Bounces;
            shot.shards = projectile.Shards;
            shot.shardObjectID = ResolveObject(projectile.ShardObjectId);

            // The shard was the one reference in this file with no report of any kind: a name the
            // game does not have baked None and the shot shattered into nothing, silently. One of
            // the MOD's own shards keeps None here on purpose and the bootstrap registers the pair.
            if (!string.IsNullOrEmpty(projectile.ShardObjectId) &&
                shot.shardObjectID == ObjectID.None &&
                !binder.IsDeferred(projectile.ShardObjectId))
            {
                report.Warnings.Add(
                    "'" + projectile.DisplayName + "' breaks into '" + projectile.ShardObjectId +
                    "', which is neither one of this mod's objects nor one the game has, so it " +
                    "breaks into nothing.");
            }

            // NAMED BUT NEVER USED. ProjectileConverter only writes the shatter component behind
            // the flag, so a shard filled in without it is a field that reaches nothing — and for
            // one of the mod's own shards the check above says nothing either, because the name is
            // perfectly good. The field is the thing that is unread, not the name.
            if (!string.IsNullOrEmpty(projectile.ShardObjectId) && !projectile.ShattersOnCollision)
            {
                report.Warnings.Add(
                    "'" + projectile.DisplayName + "' breaks into '" + projectile.ShardObjectId +
                    "' but is not set to shatter, so it never breaks into anything. Tick 'It " +
                    "shatters instead of exploding when it hits something', or clear that name.");
            }

            shot.shatterOnCollision = projectile.ShattersOnCollision;
            shot.pingPongDuration = projectile.OutAndBackSeconds;
            shot.useSpeedCurve = projectile.SpeedFollowsACurve;
            shot.speedCurve1 = projectile.SpeedCurve;
            shot.speedCurve2 = projectile.SecondSpeedCurve;
            shot.mayExplodeWithWindup = projectile.MayExplodeOnAPartialWindUp;
            shot.onlyAttackEveryXSecond = projectile.OnlyHitsTheSameThingEvery;

            shot.piercesWallTypes = new System.Collections.Generic.List<PugTilemap.Tileset>();
            string[] wallTypes = projectile.FliesThroughWallTypes;
            for (int i = 0; i < wallTypes.Length; i++)
            {
                int wallTileset = resolveTileset == null ? -1 : resolveTileset(wallTypes[i]);
                if (wallTileset >= 0)
                {
                    shot.piercesWallTypes.Add((PugTilemap.Tileset)wallTileset);
                }
                else
                {
                    report.Warnings.Add(
                        "'" + projectile.DisplayName + "' flies through '" + wallTypes[i] +
                        "', which is neither one of this mod's tilesets nor one of the game's, so " +
                        "that wall will stop it.");
                }
            }

            if (projectile.SpeedCurveWillBeIgnored)
            {
                report.Warnings.Add(
                    "'" + projectile.DisplayName + "' has a speed curve drawn on it without being " +
                    "told to follow one, so it travels at a flat speed.");
            }

            if (projectile.Shards > 0 &&
                shot.shardObjectID == ObjectID.None &&
                !string.IsNullOrEmpty(projectile.ShardObjectId))
            {
                report.Warnings.Add(
                    "'" + projectile.DisplayName + "' breaks into '" + projectile.ShardObjectId +
                    "', which is not an object the game has, so it breaks into nothing.");
            }
        }

        /// <summary>
        /// Writes the artillery half of a projectile, and takes off the straight-shot half.
        /// </summary>
        /// <remarks>
        /// A mortar carries <c>MortarProjectileAuthoring</c> INSTEAD of <c>ProjectileAuthoring</c> —
        /// measured on the falling-rock and bomb prefabs, which have no straight-shot component at
        /// all. Leaving both on would give the shell two ways to hit the same thing.
        /// </remarks>
        private static void ConfigureArtillery(
            GameObject root,
            DimensionProjectileAsset projectile,
            System.Func<string, int> resolveTileset,
            DimensionProjectileGenerationReport report)
        {
            RemoveComponentIfPresent<ProjectileAuthoring>(root);

            if (projectile.ArcsInstantly)
            {
                report.Warnings.Add(
                    "'" + projectile.DisplayName + "' arcs with its own timings and every one of " +
                    "them is zero, so it completes the whole arc in no time and goes off where it " +
                    "was fired.");
            }

            if (projectile.ScattersOrLandsWithoutNamingATileset)
            {
                report.Warnings.Add(
                    "'" + projectile.DisplayName + "' is set to scatter or leave tiles without " +
                    "naming a tileset, so it falls and leaves bare ground.");
            }

            MortarProjectileAuthoring mortar = EnsureComponent<MortarProjectileAuthoring>(root);
            mortar.radius = projectile.HitRadius;
            mortar.useDefaultTimings = projectile.UseTheGamesOwnTimings;
            mortar.goUpTime = projectile.GoUpSeconds;
            mortar.airTime = projectile.AirSeconds;
            mortar.goDownTime = projectile.GoDownSeconds;
            mortar.explodeTime = projectile.ExplodeSeconds;
            mortar.hitTiles = projectile.BreaksTerrainWhereItLands;
            mortar.isMagic = projectile.IsMagic;
            mortar.bypassMaxDamagePerHit = projectile.IgnoresTheDamageCap;
            mortar.checkVisibility = projectile.OnlyLandsWhereItCanSee;

            mortar.spawnTilesOnGoingDown = projectile.ScattersTilesOnTheWayDown;
            if (projectile.ScattersTilesOnTheWayDown)
            {
                int scattered = ResolveTilesetOrReport(
                    projectile.ScatteredTilesetId,
                    projectile.DisplayName,
                    resolveTileset,
                    report);
                if (scattered < 0)
                {
                    mortar.spawnTilesOnGoingDown = false;
                }
                else
                {
                    mortar.tileTypeToSpawnOnGoingDown = projectile.ScatteredTileType;
                    mortar.tilesetToSpawnOnGoingDown = (PugTilemap.Tileset)scattered;
                    mortar.spawnTilesOnGoingDownExtraRadius = projectile.ScatterExtraRadius;
                }
            }

            mortar.canSpawnTilesOnWaterOrPits = projectile.CanPlaceTilesOnWaterAndPits;
            mortar.randomizeEdgeForTilesToSpawn = projectile.RaggedEdges;
            mortar.pushback = projectile.PushesWhatItHits;
            mortar.isPredicted = projectile.ClientPredictsIt;
            mortar.skipWallAndRootsLootDropOnDestroy = projectile.WallsItBreaksDropNothing;
            mortar.spawnNapalmVariation = projectile.LeavesBehindVariation;
            mortar.spawnNapalmObjectID = ResolveObject(projectile.LeavesBehindObjectId);

            if (!string.IsNullOrEmpty(projectile.LeavesBehindObjectId) &&
                mortar.spawnNapalmObjectID == ObjectID.None &&
                !binder.IsDeferred(projectile.LeavesBehindObjectId))
            {
                report.Warnings.Add(
                    "'" + projectile.DisplayName + "' leaves '" + projectile.LeavesBehindObjectId +
                    "' where it lands, which is neither one of this mod's objects nor one the game " +
                    "has, so nothing will be left.");
            }

            // Taking tiles away is the mirror of putting them down, and it needs the same
            // resolve-or-back-out treatment: a tileset name the game does not have would otherwise
            // remove tileset 0 — dirt — from wherever the shell landed.
            mortar.removeTilesOnLand = projectile.RemovesTilesWhereItLands;
            if (projectile.RemovesTilesWhereItLands)
            {
                int removed = ResolveTilesetOrReport(
                    projectile.RemovedTilesetId,
                    projectile.DisplayName,
                    resolveTileset,
                    report);
                if (removed < 0)
                {
                    mortar.removeTilesOnLand = false;
                }
                else
                {
                    mortar.tileTypeToRemove = projectile.RemovedTileType;
                    mortar.tilesetToRemove = (PugTilemap.Tileset)removed;
                }
            }

            mortar.spawnTilesOnLand = projectile.LeavesATileWhereItLands;
            if (projectile.LeavesATileWhereItLands)
            {
                int landed = ResolveTilesetOrReport(
                    projectile.LandedTilesetId,
                    projectile.DisplayName,
                    resolveTileset,
                    report);
                if (landed < 0)
                {
                    mortar.spawnTilesOnLand = false;
                }
                else
                {
                    mortar.tileTypeToSpawn = projectile.LandedTileType;
                    mortar.tilesetToSpawn = (PugTilemap.Tileset)landed;
                }
            }
        }

        /// <summary>Resolves a tileset name, or -1 with a report when it is not one.</summary>
        private static int ResolveTilesetOrReport(
            string tilesetId,
            string displayName,
            System.Func<string, int> resolveTileset,
            DimensionProjectileGenerationReport report)
        {
            int resolved = resolveTileset == null ? -1 : resolveTileset(tilesetId);
            if (resolved < 0)
            {
                report.Warnings.Add(
                    "'" + displayName + "' scatters '" + tilesetId + "', which is neither one of " +
                    "this mod's tilesets nor one of the game's, so nothing will be left there.");
            }

            return resolved;
        }

        /// <summary>
        /// Builds the name-to-tileset lookup an object uses to name the tile it leaves behind.
        /// </summary>
        /// <remarks>
        /// The mod's own tilesets are checked first, so an author who names a block after a vanilla
        /// one gets their own rather than the game's. Vanilla names still resolve, because leaving
        /// ordinary dirt or stone behind is a perfectly reasonable thing to want.
        /// </remarks>
        private static System.Func<string, int> BuildTilesetResolver(
            IEnumerable<DimensionTilesetAsset> tilesets)
        {
            Dictionary<string, int> custom = new Dictionary<string, int>();
            if (tilesets != null)
            {
                foreach (DimensionTilesetAsset tileset in tilesets)
                {
                    if (tileset == null)
                    {
                        continue;
                    }

                    string name = tileset.TilesetName;
                    if (!string.IsNullOrEmpty(name) && !custom.ContainsKey(name))
                    {
                        custom[name] = tileset.TilesetId;
                    }
                }
            }

            return delegate(string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return -1;
                }

                int id;
                if (custom.TryGetValue(name, out id))
                {
                    return id;
                }

                PugTilemap.Tileset vanilla;
                if (System.Enum.TryParse(name, false, out vanilla))
                {
                    return (int)vanilla;
                }

                return -1;
            };
        }

        /// <summary>
        /// The run's binder: the game's own numbers baked, this mod's own names left for the game.
        /// </summary>
        /// <remarks>
        /// Set at the top of <c>Generate</c>. It replaced a bare enum parse with two defects: it
        /// treated <c>ObjectID.None</c> as a successful parse (<c>Enum.TryParse("None")</c>
        /// succeeds), and it had no way to tell a typo from one of the mod's own objects, so a shot
        /// that broke into the mod's own shard silently broke into nothing.
        /// </remarks>
        private static DimensionObjectBinder binder = new DimensionObjectBinder(default);

        private static ObjectID ResolveObject(string itemId)
        {
            return DimensionObjectBinder.Vanilla(itemId);
        }

        private static void RemoveComponentIfPresent<T>(GameObject root) where T : Component
        {
            // Routed through the one dependency-aware removal, so a RequireComponent cannot
            // silently defeat authoritative generation. See DimensionObjectSpine.TryRemoveComponent.
            DimensionObjectSpine.TryRemoveComponent<T>(root);
        }

        private static T EnsureComponent<T>(GameObject root) where T : Component
        {
            T existing = root.GetComponent<T>();
            return existing != null ? existing : root.AddComponent<T>();
        }

    }
}
