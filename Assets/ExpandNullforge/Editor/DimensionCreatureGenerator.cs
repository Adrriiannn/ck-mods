using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>What generating creatures did.</summary>
    internal sealed class DimensionCreatureGenerationReport
    {
        public readonly List<string> Created = new List<string>();
        public readonly List<string> Updated = new List<string>();
        public readonly List<string> Skipped = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public readonly List<string> Errors = new List<string>();

        /// <summary>
        /// Art files this run threw away because nothing in the project describes them any more.
        /// </summary>
        /// <remarks>
        /// Reported rather than done quietly. Deleting a file is the one thing a generator does
        /// that an author cannot undo by regenerating, so the paths are named where the rest of the
        /// run's outcome is named.
        /// </remarks>
        public readonly List<string> Removed = new List<string>();

        /// <summary>
        /// Drops that could not be written as custom loot and need a loot-table registration.
        /// </summary>
        /// <remarks>
        /// Carried out of the generator rather than warned about and forgotten: an amount range or a
        /// biome restriction cannot live on per-object custom loot, so these have to be registered
        /// against the source loot table at load instead. Losing them here would silently flatten
        /// exactly what the author asked for.
        /// </remarks>
        public readonly List<DimensionResolvedDrop> DropsNeedingALootTable =
            new List<DimensionResolvedDrop>();

        public bool HasProblems
        {
            get { return Errors.Count > 0 || Warnings.Count > 0; }
        }

        public string Summarize()
        {
            return "Creatures: " + Created.Count + " created, " + Updated.Count + " updated, " +
                Skipped.Count + " skipped.";
        }
    }

    /// <summary>
    /// Turns an authored creature into a real Core Keeper prefab, with the numbers the author typed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT IS BORROWED AND WHAT IS OWNED. A creature's <em>behaviour</em> — how it chases, when it
    /// attacks, what its attack looks like — is one of Core Keeper's own, named by
    /// <c>BehaviourObjectID</c>. That is not a limitation to work around: reimplementing an AI would
    /// produce something that moves and reads unlike everything else in the game. What the author owns
    /// instead is everything that makes the creature theirs: its art, its name, its stats, its drops,
    /// its reach and its speed.
    /// </para>
    /// <para>
    /// THE ONE THING THIS FIGHTS. Core Keeper derives health and damage reduction from the area level
    /// through fixed curves, and it does so at bake time in <c>OnValidate</c> — so a creature authored
    /// with 450 health silently becomes whatever the curve says the moment the prefab is touched. The
    /// generator switches those derivations OFF unless the author asked for them, which is what makes
    /// an authored number actually survive to the game.
    /// </para>
    /// </remarks>
    internal static class DimensionCreatureGenerator
    {
        /// <summary>The folder inside a mod where generated creature prefabs live.</summary>
        public const string FolderName = "Creatures";

        /// <summary>
        /// One creature to generate, flattened so mobs, bosses, animals and critters share a path.
        /// </summary>
        /// <remarks>
        /// The four authoring assets differ in what they offer an author, not in what a prefab needs.
        /// Flattening here keeps one generator honest rather than four that drift.
        /// </remarks>
        public sealed class Request
        {
            public string CreatureId = string.Empty;
            public string DisplayName = string.Empty;
            public DimensionCreatureStatsTemplate Stats;

            /// <summary>How it notices things, wanders, and fights.</summary>
            /// <remarks>
            /// Optional so existing callers keep working; when absent the creature is generated the
            /// way it was before, with no perception, no wander and no attack.
            /// </remarks>
            public DimensionCreatureCombatTemplate Combat;

            /// <summary>Loot it drops other than on death — as it is hit, on use, or by season.</summary>
            public DimensionExtraLootTemplate ExtraLoot;

            /// <summary>Whether it is a pet, and what it gives its owner.</summary>
            public DimensionPetTemplate Pet;

            /// <summary>A fighting kit borrowed from one of the game's own bosses.</summary>
            public DimensionBorrowedBossKitTemplate BorrowedKit;

            /// <summary>The Core, Wall and Scarab kits.</summary>
            public DimensionCoreBossKitTemplate MoreBorrowedKits;

            /// <summary>The Bird, Robot, Octopus, Larva, Shaman and Snake kits.</summary>
            public DimensionMoreBossKitsTemplate TheRestOfTheKits;

            /// <summary>The small things it simply is, or simply does.</summary>
            public DimensionSimpleTraitsTemplate SimpleTraits;

            public DimensionLootTableAsset LootTable;

            /// <summary>Everything that named this creature as where it drops from.</summary>
            /// <remarks>
            /// Produced by <c>DimensionDropCollector</c>; null when nothing named it.
            /// </remarks>
            public DimensionDropsForSource DropsFromItems;

            /// <summary>The Core Keeper behaviour this creature borrows, by name.</summary>
            public string BehaviourName = string.Empty;

            /// <summary>Whether it carries the game's enemy tag.</summary>
            /// <remarks>
            /// This is NOT the temperament switch, and it is not "is it dangerous". The tag brings
            /// <c>EnemyCD</c> and <c>LastAttackerCD</c> with it (<c>EnemyConverter</c>), and
            /// <c>LastAttackerCD</c> is the component that lets anything ever fight back, so a
            /// creature without it can only ever be a punchbag. The game's own Cow and Roly Poly
            /// carry <c>EnemyAuthoring</c> and are still harmless — what makes them harmless is
            /// their empty attack tags. Explosions and pushback also only reach entities with the
            /// tag, so dropping it makes a creature quietly immune to bombs.
            /// </remarks>
            public bool IsEnemy = true;

            /// <summary>Whether it ignores players, defends itself, or hunts them down.</summary>
            public DimensionSpawnAggressionKind Aggression = DimensionSpawnAggressionKind.Hostile;

            /// <summary>How far above its area's level this creature stands.</summary>
            /// <remarks>
            /// Rarity is not decoration on a creature. <c>AreaLevelAuthoring.CalculateLevel</c>
            /// reads it off this same <c>ObjectAuthoring</c> and resolves the creature's level as
            /// <c>areaLevelNumber + (int)rarity</c> (`ck-db\Pug.Base\LevelScaling.cs:100`), so on
            /// anything using the level curve this is the health-and-damage dial. Common leaves it
            /// exactly where the area puts it, which is right for every ordinary creature.
            /// </remarks>
            public Rarity Rarity = Rarity.Common;

            /// <summary>Whether it counts as a boss.</summary>
            public bool IsBoss;

            /// <summary>The treasure chest it leaves where it died. Bosses only.</summary>
            public DimensionBossChestTemplate BossChest;

            /// <summary>The pin it shows on the map. Bosses only.</summary>
            public DimensionBossMapPinTemplate MapPin;

            /// <summary>The music of its fight. Bosses only.</summary>
            public DimensionMusicAreaTemplate FightMusic;

            /// <summary>The sprite the generated view renders as its body.</summary>
            public Sprite BodySprite;

            /// <summary>What it looks like and what it is seen doing.</summary>
            /// <remarks>
            /// Optional so older callers keep generating exactly what they generated before. Absent,
            /// the creature gets no body at all — which is what every mob got before this template
            /// existed, and why they were invisible.
            /// </remarks>
            public DimensionSpawnableVisualTemplate Visual;

            /// <summary>The noises it makes.</summary>
            public DimensionSpawnableAudioTemplate Audio;

            /// <summary>The authored id of the item that summons it. Bosses only.</summary>
            public string SummoningItemId = string.Empty;

            /// <summary>Whether it is an egg that hatches when a player nears, and into what.</summary>
            public DimensionHatchingTemplate Hatching;

            /// <summary>Whether players can walk through it.</summary>
            public bool DontBlockPlayerMovement;

            public bool Enabled = true;
        }

        public static DimensionCreatureGenerationReport Generate(
            IEnumerable<Request> requests,
            string outputFolder,
            DimensionNamingContext naming)
        {
            DimensionCreatureGenerationReport report = new DimensionCreatureGenerationReport();
            if (requests == null)
            {
                return report;
            }

            if (string.IsNullOrEmpty(outputFolder))
            {
                report.Errors.Add("No output folder was resolved, so no creatures were generated.");
                return report;
            }

            DimensionAssetFolders.Ensure(outputFolder);
            binder = new DimensionObjectBinder(naming);

            List<Request> requestList = new List<Request>(requests);

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < requestList.Count; i++)
                {
                    GenerateOne(requestList[i], outputFolder, naming, report);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // Companions are a SECOND pass, once every prefab exists.
            ApplyCompanionPass(requestList, outputFolder, report);

            RemoveStaleArt(requestList, outputFolder, report);

            return report;
        }

        /// <summary>
        /// Throws away the art belonging to creatures this project no longer has, and the pictures
        /// belonging to clips it no longer has.
        /// </summary>
        /// <remarks>
        /// <para>
        /// RENAMING IS THE CASE THIS EXISTS FOR. Every one of a creature's art files is named after
        /// its id, so renaming a creature abandons all of them at once — and an abandoned sprite
        /// asset is not inert: it stays in the mod's sprite manifest, is still compiled into the
        /// game's atlas, and still holds the data-block address its old name hashed to. Without
        /// this, a mod that has been edited for a while ships several copies of every creature that
        /// was ever renamed.
        /// </para>
        /// <para>
        /// A DISABLED CREATURE KEEPS ITS ART. Disabling is a way of putting something aside, not of
        /// deleting it, and an author who ticks it back on should not find its pictures gone. So
        /// every id in the request list counts as wanted, enabled or not.
        /// </para>
        /// <para>
        /// The pictures an author picked are protected by path. A generated file may legitimately
        /// be the source of the next thing authored — <c>CopyPicture</c> supports a source that
        /// already sits at its destination — and deleting one would take away the author's only
        /// copy rather than a duplicate.
        /// </para>
        /// </remarks>
        private static void RemoveStaleArt(
            List<Request> requests,
            string outputFolder,
            DimensionCreatureGenerationReport report)
        {
            HashSet<string> wanted = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> protectedPaths = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < requests.Count; i++)
            {
                Request request = requests[i];
                if (request == null || string.IsNullOrEmpty(request.CreatureId))
                {
                    continue;
                }

                wanted.Add(DimensionCreatureSpriteAssetUtility.AssetNameFor(request.CreatureId));
                CollectPicturePaths(
                    request.Visual == null ? null : request.Visual.Animation, protectedPaths);
            }

            DimensionSpriteArtFileUtility.RemoveStaleGeneratedArt(
                outputFolder,
                wanted,
                protectedPaths,
                delegate(string path) { report.Removed.Add(path); });
        }

        /// <summary>Where every picture a creature's clips point at currently lives.</summary>
        internal static void CollectPicturePaths(
            DimensionCreatureAnimationTemplate animation,
            HashSet<string> paths)
        {
            if (animation == null)
            {
                return;
            }

            DimensionCreatureClipTemplate[] clips = animation.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                DimensionCreatureClipTemplate clip = clips[i];
                if (clip == null)
                {
                    continue;
                }

                AddPicturePath(clip.Strip, paths);
                AddPicturePath(clip.StripFromBehind, paths);
                AddPicturePath(clip.StripFromTheSide, paths);
            }
        }

        private static void AddPicturePath(Texture2D texture, HashSet<string> paths)
        {
            if (texture == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
            {
                paths.Add(path);
            }
        }


        /// <summary>
        /// Wires up the creatures that arrive alongside other creatures.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A second pass, after every prefab exists. <c>SpawnCompanionsAuthoring</c> holds real
        /// prefab references rather than names, so doing this while generating each creature would
        /// mean a creature listed before its companion finds nothing — and the order of a list in
        /// the inspector would decide whether a boss arrives with its guards.
        /// </para>
        /// <para>
        /// It opens its own asset-editing batch because the first one has already closed: the
        /// prefabs have to be on disk and loadable before any of this can resolve.
        /// </para>
        /// </remarks>
        private static void ApplyCompanionPass(
            List<Request> requests,
            string outputFolder,
            DimensionCreatureGenerationReport report)
        {
            List<Request> wantsCompany = new List<Request>();
            for (int i = 0; i < requests.Count; i++)
            {
                if (requests[i].Combat != null && requests[i].Combat.ArrivesWithCompany)
                {
                    wantsCompany.Add(requests[i]);
                }
            }

            if (wantsCompany.Count == 0)
            {
                return;
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < wantsCompany.Count; i++)
                {
                    Request request = wantsCompany[i];
                    string path =
                        outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(request.CreatureId, "Creature") + ".prefab";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    {
                        continue;
                    }

                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        DimensionObjectSpine.ApplyCompanions(
                            root,
                            request.Combat.ArrivesWith,
                            request.Combat.CompanyFollows,
                            request.Combat.CompanyAppearsAt,
                            delegate(string companionId)
                            {
                                return AssetDatabase.LoadAssetAtPath<GameObject>(
                                    outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(companionId, "Creature") + ".prefab");
                            },
                            delegate(string message)
                            {
                                report.Warnings.Add("'" + request.DisplayName + "' " + message);
                            });
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static void GenerateOne(
            Request request,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionCreatureGenerationReport report)
        {
            if (request == null || !request.Enabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(request.CreatureId))
            {
                report.Skipped.Add("A creature with no id was skipped.");
                return;
            }

            if (request.Stats == null)
            {
                report.Skipped.Add(request.CreatureId + " has no stats and was skipped.");
                return;
            }

            string prefabPath = outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(request.CreatureId, "Creature") + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(request.CreatureId);

            try
            {
                Configure(root, request, outputFolder, naming, report);
                if (request.IsBoss)
                {
                    ApplyBossExtras(root, request, outputFolder, naming, report);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

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
                report.Errors.Add(request.CreatureId + " failed to generate: " + exception.Message);
            }
            finally
            {
                if (updating)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void Configure(
            GameObject root,
            Request request,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionCreatureGenerationReport report)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            // QualifyGenerated returns the id unchanged when the owning mod could not be resolved, so
            // there is no null case to branch on — the naming context is a value, not a reference.
            obj.objectName = naming.QualifyGenerated(request.CreatureId);
            obj.objectType = ObjectType.Creature;
            obj.initialAmount = 1;

            // And the same answer as a component the RUNNING game can read. ObjectConverter, which
            // every mod object goes through, does not add ObjectTypeCD; the game's own
            // EntityMonoBehaviourDataConverter does, which is why vanilla creatures have it and
            // ours did not. It matters most here: EnvironmentalConditionsSystem queries
            // .WithAll<ObjectTypeCD>(), so a creature without the component is not merely given a
            // wrong default, it is outside the system — a custom creature could stand in slime,
            // lava or freezing water and be touched by none of it.
            EnsureComponent<ExpandNullforge.Authoring.DimensionObjectTypeAuthoring>(root);

            // Written BEFORE ApplyAreaLevel, which is what reads it: AreaLevelAuthoring's own
            // OnValidate resolves the creature's level from this object's rarity, so a rarity set
            // afterwards would not reach the health and damage the level decides.
            obj.rarity = request.Rarity;

            // WHAT THIS CREATURE COUNTS AS, to everything else's targeting. Every creature prefab
            // in the game carries HostileCreature on its own tag list — Larva and Caveling do, and
            // so do Cow and Roly Poly, which are harmless. It is not "is it dangerous", it is "is
            // it a creature", and without it a generated creature is unreachable: a player's pet
            // never chases it (ChaseStateRequest tests the TARGET's tags against the pet's
            // wantsToAttack list) and a ranged pet never shoots it at all
            // (`ck-db\Pug.Other\RangeAttackStateRequest.cs:160` skips a target with no match).
            DimensionObjectSpine.SetCategoryTag(root, ObjectCategoryTag.HostileCreature, true);

            // BEFORE THE CHASE AND THE WANDER, not at the end with the rest of the sweep. Both of
            // those passes read the creature's hitbox off the object and hand it to the state as
            // the shape the movement belongs to — and both of them ran on a creature that had no
            // hitbox at all, so both bound null every time. The body itself matters for more than
            // that: ten movement systems name the velocity a dynamic body produces, the chase and
            // the charge both refuse a creature with no collider outright, and nothing else's
            // overlap can find one either.
            // SIZED FROM THE CREATURE, not from a constant. Both call sites used to hand in a
            // hard 1, so the parameters were dead and every creature in every mod was the same
            // 0.75 across whatever it looked like. The two numbers are separate because 24 of the
            // 63 vanilla creatures that carry both shapes size them differently.
            DimensionQueryCompanions.HasABodyThingsCanTouch(
                root,
                request.Stats == null ? 1f : request.Stats.BodyWidthInTiles,
                1f,
                request.Stats == null ? 1f : request.Stats.NoticedWidthInTiles,
                delegate(string message)
                {
                    report.Warnings.Add("'" + request.DisplayName + "' " + message);
                });

            ApplyIdentity(root, request, report);
            ApplyCreatureView(root, request, outputFolder, naming, report);
            ApplyHatching(root, request, naming, report);
            ApplyHealth(root, request);
            ApplyDamageReduction(root, request);
            ApplyMovement(root, request);
            ApplyChase(root, request, report);
            ApplyDeath(root, request);
            ApplyBehaviour(root, request, report);
            ApplyLoot(root, request, report);

            // Everything a creature needs to actually behave like one. Guarded so callers that predate
            // the combat template keep generating exactly what they generated before.
            if (request.Combat != null)
            {
                ApplyStateMachine(root, request);
                ApplyPerception(root, request.Combat, request, report);
                DimensionObjectSpine.ApplyMoreBossKits(
                    root,
                    request.TheRestOfTheKits,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                DimensionObjectSpine.ApplyCoreBossKit(
                    root,
                    request.MoreBorrowedKits,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                DimensionObjectSpine.ApplyBorrowedBossKit(
                    root,
                    request.BorrowedKit,
                    null,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                DimensionObjectSpine.ApplyMoreCombat(
                    root,
                    request.Combat.MoreCombat,
                    delegate(string objectId) { return ResolveObject(objectId); },
                    null,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                DimensionObjectSpine.ApplySmashesObjects(
                    root,
                    request.Combat.SmashesObjects,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                DimensionObjectSpine.ApplySegmentedCreature(
                    root,
                    request.Combat.Segmented,
                    delegate(string objectId) { return ResolveObject(objectId); },
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    },
                    IsDeferred);

                DimensionObjectSpine.ApplyPatrolPath(
                    root,
                    request.Combat.Patrol,
                    null,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                DimensionObjectSpine.ApplyCreatureHabits(
                    root,
                    request.Combat.Habits,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });
                ApplyIdleMovement(root, request.Combat);
                DimensionObjectSpine.ApplyAttackSounds(
                    root,
                    request.Combat.AttackSounds,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });
            }

            if (request.SimpleTraits != null)
            {
                DimensionObjectSpine.ApplySimpleTraits(
                    root,
                    request.SimpleTraits,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });
            }

            if (request.Combat != null)
            {
                // ApplyStateMachine ran above and has already decided whether this creature carries
                // a tier at all (ApplyAreaLevel, on the stat source). Saying so here is what stops
                // Basics putting back what the stat source deliberately took off.
                DimensionObjectSpine.ApplyBasics(
                    root,
                    request.Combat.Basics,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    },
                    request.Stats == null || !request.Stats.UsesAuthoredNumbers);
                DimensionObjectSpine.ApplyInitialConditions(
                    root,
                    request.Combat.Conditions,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                DimensionObjectSpine.ApplyCreatureLifecycle(
                    root,
                    request.Combat.Lifecycle,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                DimensionObjectSpine.ApplyGravityWells(
                    root,
                    request.Combat.GravityWellChance,
                    request.Combat.GravityWellStrength,
                    request.Combat.GravityWellRange,
                    request.Combat.GravityWellMaxTurn,
                    request.Combat.GravityWellAttractsLayers);
                DimensionObjectSpine.ApplyImmunities(
                    root,
                    request.Combat.ImmuneToPushBack,
                    request.Combat.ImmuneToRangedDamage,
                    false);
                DimensionObjectSpine.ApplyLeavesBehind(
                    root,
                    request.Combat.LeavesBehind,
                    delegate(string objectId) { return ResolveObject(objectId); },
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });
                ApplyAttacks(root, request.Combat, request, report);
                ApplyAbilities(root, request.Combat, request, report);

                // ---- THE SPINE THE CREATURE GENERATOR NEVER CALLED --------------------------
                //
                // Eight passes below, every one of them already written, already public static,
                // already handling a null template — and every one of them reachable from the
                // world-object generator alone. A beam, a healing aura, mana, an owner, hiding in
                // bushes, roaming a circuit, hurting whatever comes near and a shop were all
                // buildable, and none of them was reachable from the content type they belong to.
                //
                // WHY HERE AND NOT SOMEWHERE TIDIER. Two rules pin the position:
                //  * AFTER ApplyCreatureHabits (:610), because MinionConverter reads
                //    IsFlyingAuthoring off the object and the habits are what write it, and because
                //    ApplyPatrolPath (:602) has to have written the route before the sweep is asked
                //    whether roaming has one.
                //  * BEFORE ApplyTemperament (below), because the temperament overwrites the attack
                //    tags and the chase distance and may remove BehaviourTagsAuthoring outright,
                //    and because FinishACreature runs after THAT and is what closes the query gaps
                //    these passes open. Moving any of this after the temperament silently undoes
                //    the sweep.
                //
                // None of the eight touches MealsEatenAuthoring, which is the one component in this
                // stretch with a documented ordering trap (the habits toggle it, ApplyAbilities
                // force-adds it for Evolve, and the force-add has to win). DimensionCreatureSpineTests
                // asserts that they still do not.
                //
                // TWO OF THE EIGHT SHARE ONE COMPONENT AND THE ORDER BETWEEN THEM IS DELIBERATE.
                // ApplyContinuousAttack owns AttackContinuouslyAuthoring and removes it when the
                // answer is off; ApplyFinalTouches ADDS the same component for a boss beam, because
                // a beam in Core Keeper is a thing that keeps hurting whatever stands in it and the
                // beam's system skips one without it. Continuous attack goes first so the boss
                // beam's add is the last word. Swapping them makes a boss beam inert.
                DimensionObjectSpine.ApplyContinuousAttack(
                    root,
                    request.Combat.ContinuousAttack,
                    null,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                DimensionObjectSpine.ApplyBeamAndAmbience(
                    root,
                    request.Combat.BeamAndAmbience,
                    delegate(string objectId) { return ResolveObject(objectId); },
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                // THE LAST ARGUMENT STOPS THIS DELETING EVERY EGG. ApplyHatching (:533) owns
                // HatchWhenPlayerNearbyStateAuthoring on a creature and is the only one of the two
                // that can name one of THIS mod's creatures to hatch into — it writes the name
                // beside the id for hydration to fill in. This pass runs later and its else-branch
                // removes that component, so without the flag a generated egg would be built and
                // then quietly stripped of its hatching in the same run.
                DimensionObjectSpine.ApplyHidingAndHatching(
                    root,
                    request.Combat.HidingAndHatching,
                    delegate(string objectId) { return ResolveObject(objectId); },
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    },
                    true);

                DimensionObjectSpine.ApplyManaAndAura(
                    root,
                    request.Combat.ManaAndAura,
                    delegate(string objectId) { return ResolveObject(objectId); },
                    null,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                DimensionObjectSpine.ApplyFinalTouches(
                    root,
                    request.Combat.FinalTouches,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                // The last argument is the whole reason this call is different from the world
                // object's: two of the notes inside tell a PLACED thing that wandering and roaming
                // are creature work and to build it as a creature, which would be a flat lie here.
                DimensionObjectSpine.ApplySpawnerAndOrb(
                    root,
                    request.Combat.SpawnerAndOrb,
                    delegate(string objectId) { return ResolveObject(objectId); },
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    },
                    true);

                DimensionObjectSpine.ApplyTrader(
                    root,
                    request.Combat.Trader,
                    delegate(string objectId) { return ResolveObject(objectId); },
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    },
                    IsDeferred);

                DimensionObjectSpine.ApplyCreatureMinion(
                    root,
                    request.Combat.Minion,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                DimensionObjectSpine.ApplyCreatureLastStand(
                    root,
                    request.Combat.LastStand,
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                WarnAboutCombat(request, report);
            }

            // LAST, and outside the combat guard. It overwrites the attack tags and the chase
            // distance the steps above wrote, and its warnings need to see whether an attack was
            // actually added — both of which are only true once everything else has run.
            ApplyTemperament(root, request, report);

            // AFTER EVEN THAT. The three things every one of the game's own creatures carries and
            // none of ours did — being sent to players, having a body, being able to turn — plus
            // the sweep for anything else on the creature whose system needs a component beside it.
            // It can only run once every pass has finished, because what it adds depends on what
            // the finished creature turned out to be.
            DimensionQueryCompanions.FinishACreature(
                root,
                request.DisplayName,
                request.Stats == null ? 1f : request.Stats.BodyWidthInTiles,
                request.Stats == null ? 1f : request.Stats.NoticedWidthInTiles,
                delegate(string message)
                {
                    report.Warnings.Add(message);
                });
        }

        /// <summary>
        /// Says the quiet things out loud before the build.
        /// </summary>
        /// <remarks>
        /// None of these are errors — the creature generates and the game runs. They are the shapes
        /// that produce a creature which looks finished and does nothing, which is the expensive kind
        /// of mistake because it only shows up when someone plays the mod.
        /// </remarks>
        private static void WarnAboutCombat(Request request, DimensionCreatureGenerationReport report)
        {
            DimensionCreatureCombatTemplate combat = request.Combat;

            if (combat.ChasesButCannotAttack &&
                request.Aggression == DimensionSpawnAggressionKind.Hostile)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' is an enemy with no attack. It will chase the " +
                    "player and then stand next to them doing nothing.");
            }

            if (combat.RangedIsMissingProjectile)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' is set to shoot but has no projectile, so it will " +
                    "play the shooting animation and fire nothing.");
            }

            if (combat.CannotSeeFarEnoughToShoot)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' cannot see (" + combat.DetectionRadius +
                    ") as far as its closest firing distance (" + combat.RangedMinDistance +
                    "), so by the time it notices anything it is already too close to shoot.");
            }

            // WHAT THE TENDING WINDOW READS. CattleUI works out how hungry an animal is from
            // EatStateCD.maxFoodUntilFull (`ck-db\Pug.Other\CattleUI.cs:87-89`), and it only shows
            // the breeding switch for an animal carrying BreedToggleCD (`:45`). Neither of those is
            // part of being livestock — they come from the eating and breeding abilities — so an
            // animal with neither opens a window with nothing in it but a name box.
            bool eats = HasAbility(combat, DimensionCreatureAbilityKind.Eat);
            bool breeds = HasAbility(combat, DimensionCreatureAbilityKind.Breed);

            if (combat.Habits.IsLivestock && !eats && !breeds)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' can be tended, and has no eating ability. The " +
                    "tending window works out how hungry an animal is from how much food fills it " +
                    "up, so this one will open a window that never shows it as hungry and never " +
                    "takes a meal. Add the Eat ability.");
            }

            if (breeds && !eats)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' breeds and was given no eating ability. Core " +
                    "Keeper only lets an animal go looking for a mate once it is full, and full is " +
                    "measured against how much food fills it up, so one was filled in for it with " +
                    "the game's own cow's numbers. Add the Eat ability to choose your own.");
            }

            // Only when the author gave it no chase of its own. ApplyChase writes one whenever a
            // chase distance, a chase speed or any pursuit setting is filled in, and the companion
            // row never overwrites what is already there.
            bool alreadyChases =
                (request.Stats != null &&
                 (request.Stats.HasChaseAtDistance || request.Stats.HasChaseSpeedMultiplier)) ||
                combat.Pursuit.HasAnySetting;

            if (breeds && !alreadyChases &&
                request.Aggression == DimensionSpawnAggressionKind.Passive)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' breeds and is passive. Core Keeper walks an " +
                    "animal to its mate with a chase, and a passive creature is not given one, so " +
                    "a chase was filled in with the game's own cow's numbers. It will not attack " +
                    "anything with it: a passive creature's list of what it wants to attack is " +
                    "empty, and the chase only picks from that list.");
            }
        }

        /// <summary>Whether the author gave the creature one of the named abilities.</summary>
        private static bool HasAbility(
            DimensionCreatureCombatTemplate combat,
            DimensionCreatureAbilityKind kind)
        {
            DimensionCreatureAbility[] abilities = combat.Abilities;
            for (int i = 0; i < abilities.Length; i++)
            {
                if (abilities[i] != null && abilities[i].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ApplyIdentity(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            if (request.IsEnemy)
            {
                EnemyAuthoring enemy = EnsureComponent<EnemyAuthoring>(root);
                enemy.dontBlockPlayerMovement = request.DontBlockPlayerMovement;
            }
            else
            {
                RemoveComponentIfPresent<EnemyAuthoring>(root);
            }

            if (request.IsBoss)
            {
                BossAuthoring boss = EnsureComponent<BossAuthoring>(root);
                DimensionBossChestTemplate chest =
                    request.BossChest ?? new DimensionBossChestTemplate();

                ObjectID chestObject = ResolveObject(chest.ChestObjectId);

                // One of the mod's own chests has no number yet; the bootstrap registers the row
                // and the link hydration writes BossCD.chestToSpawn at load. The AMOUNT has to be
                // the authored one even so — a chest whose amount stayed at zero would be written
                // in and still never appear.
                bool chestIsOneOfOurs =
                    chestObject == ObjectID.None && IsDeferred(chest.ChestObjectId);
                if (chestObject == ObjectID.None && !chestIsOneOfOurs && chest.LeavesAChest)
                {
                    report.Warnings.Add(
                        "'" + request.DisplayName + "' leaves '" + chest.ChestObjectId +
                        "' behind, which is neither one of this mod's objects nor one the game " +
                        "has, so it leaves no chest.");
                }

                boss.chestToSpawn = new ObjectData
                {
                    objectID = chestObject,
                    variation = chest.ChestVariation,
                    amount = chestObject == ObjectID.None && !chestIsOneOfOurs
                        ? 0
                        : chest.ChestAmount
                };
                boss.chestSpawnOffset = new Unity.Mathematics.float3(
                    chest.ChestOffset.x,
                    chest.ChestOffset.y,
                    chest.ChestOffset.z);
                boss.isMainStoryBoss = chest.IsAMainStoryBoss;

                boss.spawnOptionalChest = chest.LeavesASecondChest;
                if (chest.LeavesASecondChest)
                {
                    ObjectID secondChest = ResolveObject(chest.SecondChestObjectId);
                    if (secondChest == ObjectID.None &&
                        !IsDeferred(chest.SecondChestObjectId))
                    {
                        boss.spawnOptionalChest = false;
                        report.Warnings.Add(
                            "'" + request.DisplayName + "' leaves a second chest '" +
                            chest.SecondChestObjectId + "', which is neither one of this mod's " +
                            "objects nor one the game has, so it leaves only the first.");
                    }
                    else
                    {
                        boss.optionalChestVersion = new ObjectData
                        {
                            objectID = secondChest,
                            variation = chest.SecondChestVariation,
                            amount = 1
                        };
                    }
                }
            }
            else
            {
                RemoveComponentIfPresent<BossAuthoring>(root);
            }
        }

        /// <summary>
        /// Makes the creature an egg, exactly the way the Larva Hive's cocoons are: one
        /// component whose spawn target hatches out when a player nears.
        /// </summary>
        /// <remarks>
        /// The target bakes as an id when the game knows the name (a vanilla creature), and as
        /// a NAME beside it otherwise — the mod's own creatures have no ids at generation
        /// time, and the hydration system writes the real id on the first server ticks, long
        /// before anything can stand within five tiles of the egg.
        /// </remarks>
        private static void ApplyHatching(
            GameObject root,
            Request request,
            DimensionNamingContext naming,
            DimensionCreatureGenerationReport report)
        {
            DimensionHatchingTemplate hatching = request.Hatching;
            if (hatching == null || !hatching.Hatches)
            {
                RemoveComponentIfPresent<HatchWhenPlayerNearbyStateAuthoring>(root);
                RemoveComponentIfPresent<ExpandNullforge.Creatures.DimensionHatchTargetAuthoring>(root);
                return;
            }

            HatchWhenPlayerNearbyStateAuthoring hatch =
                EnsureComponent<HatchWhenPlayerNearbyStateAuthoring>(root);
            hatch.timeToHatch = hatching.SecondsToHatch;
            hatch.minSpawnAmount = hatching.HowMany.x;
            hatch.maxSpawnAmount = hatching.HowMany.y;

            ObjectID target = ResolveObject(hatching.WhatHatchesOut);
            hatch.objectToSpawn = target;
            if (target == ObjectID.None && IsDeferred(hatching.WhatHatchesOut))
            {
                // One of the mod's own; the name rides along and hydration fills the id.
                EnsureComponent<ExpandNullforge.Creatures.DimensionHatchTargetAuthoring>(root)
                    .spawnObjectName = naming.QualifyGenerated(hatching.WhatHatchesOut);
            }
            else
            {
                RemoveComponentIfPresent<ExpandNullforge.Creatures.DimensionHatchTargetAuthoring>(root);

                // A name that is neither the game's nor one of ours used to be carried anyway, so
                // the egg shipped with a qualified nonsense name and said nothing until the game
                // gave up on it hours later.
                if (target == ObjectID.None && !string.IsNullOrEmpty(hatching.WhatHatchesOut))
                {
                    report.Warnings.Add(
                        "'" + request.DisplayName + "' hatches into '" + hatching.WhatHatchesOut +
                        "', which is neither one of this mod's creatures nor one the game has, so " +
                        "nothing comes out of it.");
                }
            }
        }

        /// <summary>
        /// Everything that makes a boss FEEL like one of the game's own: fight music on the
        /// entity, a view with a floating name, a map pin object, and a summoning circle.
        /// </summary>
        /// <remarks>
        /// Runs inside GenerateOne's try, before the boss prefab saves, because the view prefab
        /// reference must land in <c>graphicalPrefab</c> on the root that is about to be saved.
        /// The companion prefabs pause the asset batch for their own saves — inside a batch,
        /// SaveAsPrefabAsset returns null, and here the returned reference is the point.
        /// </remarks>
        private static void ApplyBossExtras(
            GameObject root,
            Request request,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionCreatureGenerationReport report)
        {
            DimensionObjectSpine.ApplyMusicArea(
                root,
                request.FightMusic,
                message => report.Warnings.Add("'" + request.DisplayName + "' " + message));

            DimensionBossMapPinTemplate pin = request.MapPin ?? new DimensionBossMapPinTemplate();
            if (pin.PinGoesWhenItDies)
            {
                // Not left to the SimpleTraits toggle: a pin that survives its boss is the
                // silent failure, so bossness itself carries the death link.
                EnsureComponent<DisableMapMarkerOnDeathAuthoring>(root);
            }

            if (pin.ShowsOnTheMap)
            {
                BuildBossMapMarkerPrefab(request, pin, outputFolder, naming, report);
            }

            if (!string.IsNullOrEmpty(request.SummoningItemId))
            {
                BuildBossSummonCirclePrefab(request, outputFolder, naming, report);
            }
        }

        /// <summary>
        /// Gives every creature — not only bosses — something the game can actually draw.
        /// </summary>
        /// <remarks>
        /// <para>
        /// THIS RUNS FOR EVERY KIND, WHICH IS THE POINT. Until this existed only the boss branch
        /// ever assigned a graphical prefab, so every mob, animal and critter this framework
        /// generated spawned invisible: the entity was receiving "attack", "move" and "idle" from
        /// the server the whole time and there was nothing on the other end to play them.
        /// </para>
        /// <para>
        /// The art is built first because the view has to point at it, and the view prefab has to
        /// be on disk before its reference can land in <c>graphicalPrefab</c> on the creature that
        /// is about to be saved. A boss with no clips still gets a view — it has a nameplate and a
        /// still picture to show — but a mob with none gets no prefab at all rather than an empty
        /// one, so the warning is the only thing it ends up with.
        /// </para>
        /// </remarks>
        private static void ApplyCreatureView(
            GameObject root,
            Request request,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionCreatureGenerationReport report)
        {
            Action<string> warn = delegate(string message)
            {
                report.Warnings.Add("'" + request.DisplayName + "' " + message);
            };

            DimensionCreatureAnimationTemplate animation =
                request.Visual != null ? request.Visual.Animation : null;
            DimensionCreatureSpriteAssetResult art = DimensionCreatureSpriteAssetUtility.Build(
                request.CreatureId,
                naming.QualifyGenerated(request.CreatureId),
                animation,
                outputFolder,
                warn);

            if (!art.HasAsset && !request.IsBoss)
            {
                // Cleared rather than left alone: an author who deletes the last clip has said the
                // creature has no body, and a prefab left over from the previous generation would
                // keep drawing art nothing in the project still describes.
                root.GetComponent<ObjectAuthoring>().graphicalPrefab = null;
                warn(
                    "has nothing drawn for it, so it will be invisible in the world. Add at least " +
                    "a 'Standing' clip to its Visual template.");
                return;
            }

            WarnAboutTheDeathClip(request, animation, warn);

            DimensionCreatureViewBuilder.Role role = WhatKindOfBodyItNeeds(request, warn);

            DimensionCreatureViewBuilder.Result built = DimensionCreatureViewBuilder.Build(
                request.CreatureId,
                animation,
                art,
                role,
                warn);

            try
            {
                if (request.IsBoss)
                {
                    ApplyBossNamePlate(built, request, art, warn);
                }

                WireWalkingUpToIt(root, built, role, request, warn);

                string viewPath =
                    outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(request.CreatureId, "Creature") + "Visual.prefab";
                GameObject savedView = SaveCompanionPrefab(built.Root, viewPath);
                if (savedView == null)
                {
                    warn("could not save its view prefab, so it has no body this build.");
                    return;
                }

                root.GetComponent<ObjectAuthoring>().graphicalPrefab = savedView;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(built.Root);
            }
        }

        /// <summary>
        /// Which kind of body this creature needs, from its own answers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The component type is the pool, so this is not cosmetic: a creature drawn with the wrong
        /// one is handed another creature's instance. A boss wins over both windows because its
        /// nameplate is built into its own type; an animal wins over a shop because tending is the
        /// answer that comes with a name tag and a leash, and Core Keeper has no window that is
        /// both.
        /// </para>
        /// <para>
        /// Read off the two answers that already exist rather than a third tickbox: an animal opens
        /// the tending window because it is livestock, under its habits, and a creature opens the
        /// trading window because it has something to sell, under its shop. A third control saying
        /// the same thing is how two controls end up disagreeing.
        /// </para>
        /// </remarks>
        private static DimensionCreatureViewBuilder.Role WhatKindOfBodyItNeeds(
            Request request,
            Action<string> warn)
        {
            bool tended = request.Combat != null && request.Combat.Habits.IsLivestock;
            bool trades = request.Combat != null && request.Combat.Trader.IsATrader;

            if (request.IsBoss)
            {
                if (tended || trades)
                {
                    warn(
                        "is a boss and is also set to be tended or to trade. A boss carries its own " +
                        "floating name, and Core Keeper has no body that does both, so it was built " +
                        "as a boss and nobody will be able to walk up to it.");
                }

                return DimensionCreatureViewBuilder.Role.Boss;
            }

            if (tended && trades)
            {
                warn(
                    "is livestock and a trader at once. Core Keeper has one window per creature, " +
                    "so it was built as an animal a player can tend, and its shop will never open. " +
                    "Untick livestock if it is meant to be a trader.");
            }

            if (tended)
            {
                return DimensionCreatureViewBuilder.Role.TendedAnimal;
            }

            return trades
                ? DimensionCreatureViewBuilder.Role.TalkingCreature
                : DimensionCreatureViewBuilder.Role.Plain;
        }

        /// <summary>
        /// Gives a creature the half of "walking up to it" that lives on the body, and the half
        /// that lives on the entity.
        /// </summary>
        /// <remarks>
        /// <para>
        /// BOTH HALVES OR NEITHER. <c>LocalInteractableConverter</c> reads the graphical prefab's
        /// first <c>InteractableObject</c>, counts the persistent listeners on its two event lists,
        /// and logs "No local interaction events registered on entity" when it finds none — so the
        /// authoring component on the entity is only ever added when the body really did get the
        /// wiring. And <c>CreateGraphicalObjectSystem</c> fills
        /// <c>InteractableObjectReferenceCD</c> from the view's own field, which is what the utility
        /// assigns. Those two lines are the whole of what opens a window.
        /// </para>
        /// <para>
        /// The tending window itself is not an ECS query at all: <c>Cattle.Interact</c> calls
        /// <c>Manager.main.player.SetActiveCattle(this)</c> and <c>CattleUI</c> reads
        /// <c>player.activeCattle</c>. The ECS half of being an animal — <c>CattleCD</c> and
        /// <c>LeashedCD</c> — comes from <c>CattleAuthoring</c>, which the creature's habits write
        /// at <c>DimensionObjectSpine.ApplyCreatureHabits</c>, beside <c>NameAuthoring</c>.
        /// </para>
        /// <para>
        /// TAKEN OFF AGAIN when the creature is neither, because an author who unticks livestock
        /// would otherwise leave a component behind that makes the game log that same error on
        /// every spawn.
        /// </para>
        /// </remarks>
        private static void WireWalkingUpToIt(
            GameObject root,
            DimensionCreatureViewBuilder.Result built,
            DimensionCreatureViewBuilder.Role role,
            Request request,
            Action<string> warn)
        {
            if (role != DimensionCreatureViewBuilder.Role.TendedAnimal &&
                role != DimensionCreatureViewBuilder.Role.TalkingCreature)
            {
                DimensionObjectSpine.TryRemoveComponent<Interaction.LocalInteractableAuthoring>(root);
                return;
            }

            DimensionUseBehaviour what =
                role == DimensionCreatureViewBuilder.Role.TendedAnimal
                    ? DimensionUseBehaviour.TendedLikeAnAnimal
                    : DimensionUseBehaviour.TalkedToLikeAnNpc;

            DimensionInteractionVisualUtility.ApplyToACreatureView(
                built.Root,
                built.Behaviour,
                what,
                request.Combat == null ? null : request.Combat.Tending,
                warn);

            if (root.GetComponent<Interaction.LocalInteractableAuthoring>() == null)
            {
                root.AddComponent<Interaction.LocalInteractableAuthoring>();
            }
        }

        /// <summary>
        /// Says so when a death clip was drawn that nothing will ever ask for.
        /// </summary>
        /// <remarks>
        /// Dying is the one clip no state system fires. The game plays it client-side, and only for
        /// an entity carrying the component that says it has a death animation — which is written
        /// by the death state, from the author's own "skip it" switch. So a drawn death clip
        /// reaches the screen only if the creature has a death state and did not ask to skip it,
        /// and both of those are set a long way from the clip list.
        /// </remarks>
        private static void WarnAboutTheDeathClip(
            Request request,
            DimensionCreatureAnimationTemplate animation,
            Action<string> warn)
        {
            if (animation == null ||
                animation.ClipFor(DimensionCreatureClipKind.Dying) == null)
            {
                return;
            }

            if (request.Stats != null && request.Stats.SkipDeathAnimation)
            {
                warn(
                    "drew a 'Dying' clip and also asked to skip its death animation, so it will " +
                    "vanish instead of playing it. Turn that switch off on its Stats.");
                return;
            }

            if (request.Combat == null)
            {
                warn(
                    "drew a 'Dying' clip but has nothing filled in under Attacks, so it never gets " +
                    "the death state that plays one. It will vanish instead.");
            }
        }

        /// <summary>
        /// Adds the floating name only a boss carries, and the still picture behind it.
        /// </summary>
        /// <remarks>
        /// The still picture is added ONLY when the boss has no clips. A boss that has both would
        /// draw its animated body and a frozen copy of itself in the same place.
        /// </remarks>
        private static void ApplyBossNamePlate(
            DimensionCreatureViewBuilder.Result built,
            Request request,
            DimensionCreatureSpriteAssetResult art,
            Action<string> warn)
        {
            ExpandNullforge.Creatures.DimensionBossView view =
                built.View as ExpandNullforge.Creatures.DimensionBossView;
            if (view == null)
            {
                return;
            }

            if (!art.HasAsset && request.BodySprite == null)
            {
                warn(
                    "has no clips and no body picture, so only its floating name will show. " +
                    "Add a 'Standing' clip to its Visual template.");
            }

            // Always present, never always shown. Every boss shares one pool, so an instance built
            // for a boss with no clips is handed to one that has them; the renderer has to exist on
            // both prefabs for the view to be able to switch it off on the boss that animates.
            SpriteRenderer body = built.Root.AddComponent<SpriteRenderer>();
            body.sprite = art.HasAsset ? null : request.BodySprite;
            body.enabled = !art.HasAsset && request.BodySprite != null;
            view.body = body;

            // The exact nameplate the game ships on its own named boss, value for value, and in the
            // same shape: a container pushed up and toward the camera with the text a child under
            // it. Two things were wrong with building it flat onto the root here, and both were
            // invisible offline. The text sat at y -0.5, roughly at the boss's feet rather than
            // three units above its head; and the object was left on the default layer, so
            // PugFont.Render stamped every glyph it pooled onto the default layer instead of
            // WorldUI, which is the layer every world-space text in the game is drawn on.
            view.nameText = DimensionFloatingTextUtility.AddBossNameAboveIt(built.Root);
        }

        /// <summary>
        /// The map pin object, mirroring vanilla's dedicated boss-marker prefabs: visible from
        /// anywhere, scannable so death removes it, boss linked by name for runtime hydration.
        /// </summary>
        private static void BuildBossMapMarkerPrefab(
            Request request,
            DimensionBossMapPinTemplate pin,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionCreatureGenerationReport report)
        {
            if (pin.LargeMapIcon == null && pin.MiniMapIcon == null)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' shows a map pin with no icons. The pin will " +
                    "appear and carry its name on hover, but it has no picture.");
            }

            GameObject markerRoot = new GameObject(request.CreatureId + "MapMarker");
            try
            {
                ObjectAuthoring obj = markerRoot.AddComponent<ObjectAuthoring>();
                // The suffix is shared with DimensionGeneratedObjectIds.ObjectsMade, which is what
                // tells the world-load check this pin exists. Two copies of the string would drift,
                // and a drifted one reads as an id nothing answers to.
                obj.objectName = naming.QualifyGenerated(
                    request.CreatureId + DimensionGeneratedObjectIds.BossMapMarkerSuffix);
                obj.objectType = ObjectType.PlaceablePrefab;
                obj.initialAmount = 1;

                MapMarkerAuthoring marker = markerRoot.AddComponent<MapMarkerAuthoring>();
                marker.mapMarkerType = MapMarkerType.UniqueBoss;
                // The boss id does not exist at generation time; hydration writes it at runtime.
                marker.uniqueMarkerId = ObjectID.None;
                marker.hideWhenDiscovered = false;

                // Why the pin shows from anywhere on the map, exactly like vanilla's.
                markerRoot.AddComponent<OverrideNetworkSyncDistanceAuthoring>().distance =
                    float.PositiveInfinity;
                markerRoot.AddComponent<CustomDisableAuthoring>().alwaysEnabled = true;

                // The death linkage: the game disables every scannable entity naming the dead
                // boss's object, and this is what makes the pin one of them.
                CanBeScannedAuthoring scan = markerRoot.AddComponent<CanBeScannedAuthoring>();
                scan.objectData = new ObjectData { objectID = ObjectID.None, variation = 0, amount = 0 };

                markerRoot.AddComponent<ExpandNullforge.Creatures.DimensionBossMarkerAuthoring>()
                    .bossObjectName = naming.QualifyGenerated(request.CreatureId);
                markerRoot.AddComponent<Unity.NetCode.GhostAuthoringComponent>();

                // The sweep, over the pin itself. Nothing it carries has a row today, so this adds
                // nothing right now — it is here so that a row added later reaches this prefab
                // instead of only the creature's.
                DimensionQueryCompanions.CloseTheGaps(
                    markerRoot,
                    request.DisplayName + "'s map pin",
                    delegate(string message) { report.Warnings.Add(message); });

                string markerPath =
                    outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(request.CreatureId, "Creature") + "MapMarker.prefab";
                if (SaveCompanionPrefab(markerRoot, markerPath) == null)
                {
                    report.Warnings.Add(
                        "'" + request.DisplayName + "' could not save its map marker prefab.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(markerRoot);
            }
        }

        /// <summary>
        /// The arena summoning circle: the game's own numbers (the Glurch circle's), the boss
        /// linked by name. Drop it into the arena scene; the summoning item wakes it.
        /// </summary>
        private static void BuildBossSummonCirclePrefab(
            Request request,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionCreatureGenerationReport report)
        {
            GameObject circleRoot = new GameObject(request.CreatureId + "SummonCircle");
            try
            {
                ObjectAuthoring obj = circleRoot.AddComponent<ObjectAuthoring>();
                // Shared with DimensionGeneratedObjectIds.ObjectsMade for the same reason as the
                // map pin above: this circle is the object the companion table was written for, and
                // it can only be checked if both sides spell it the same way.
                obj.objectName = naming.QualifyGenerated(
                    request.CreatureId + DimensionGeneratedObjectIds.BossSummonCircleSuffix);
                obj.objectType = ObjectType.PlaceablePrefab;
                obj.initialAmount = 1;

                SummonAreaAuthoring area = circleRoot.AddComponent<SummonAreaAuthoring>();
                area.bossToSummon = ObjectID.None;
                area.optionalBossToSummon = ObjectID.None;
                area.anticipationTime = 3f;
                area.spawnTime = 0.1f;
                area.distanceToDestroyTilesOnSpawn = 3;

                // WITHOUT THIS THE CIRCLE NEVER RUNS. See
                // DimensionObjectSpine.MakeTheCircleNoticeTheItem: the game's summoning system only
                // looks at circles that can see what is lying on them and that can play an
                // animation, and neither comes with SummonAreaAuthoring.
                DimensionObjectSpine.MakeTheCircleNoticeTheItem(circleRoot, 0f);

                circleRoot.AddComponent<ExpandNullforge.Creatures.DimensionSummonAreaByNameAuthoring>()
                    .bossObjectName = naming.QualifyGenerated(request.CreatureId);
                circleRoot.AddComponent<Unity.NetCode.GhostAuthoringComponent>();

                // AND THE SWEEP, over the circle itself. The circle is a prefab of its own and the
                // sweep only ever ran over the creature, so the one object the whole companion
                // table was written for was the one object it never touched — it was patched by
                // the hand call above instead, and a row added later would have reached neither.
                DimensionQueryCompanions.CloseTheGaps(
                    circleRoot,
                    request.DisplayName + "'s summoning circle",
                    delegate(string message) { report.Warnings.Add(message); });

                string circlePath =
                    outputFolder + "/" + DimensionGeneratedPrefabUtility.SanitizeAuthoredName(request.CreatureId, "Creature") + "SummonCircle.prefab";
                if (SaveCompanionPrefab(circleRoot, circlePath) == null)
                {
                    report.Warnings.Add(
                        "'" + request.DisplayName + "' could not save its summoning circle prefab.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(circleRoot);
            }
        }

        /// <summary>
        /// Saves a companion prefab while the generator's asset batch is open.
        /// </summary>
        /// <remarks>
        /// Inside StartAssetEditing, SaveAsPrefabAsset returns null because the asset has not
        /// imported yet — fatal wherever the returned reference is the point. The batch pauses
        /// for exactly this one save; the pause is balanced, so the generator's own
        /// StopAssetEditing still closes the window it opened.
        /// </remarks>
        private static GameObject SaveCompanionPrefab(GameObject root, string path)
        {
            AssetDatabase.StopAssetEditing();
            try
            {
                bool saved;
                GameObject written = PrefabUtility.SaveAsPrefabAsset(root, path, out saved);
                if (saved && written != null)
                {
                    return written;
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            finally
            {
                AssetDatabase.StartAssetEditing();
            }
        }

        /// <summary>
        /// Writes the creature's health, and stops the game recomputing it behind the author's back.
        /// </summary>
        /// <remarks>
        /// <c>dontCalculateHealthFromLevel</c> is the whole point of this method. Left false, Core
        /// Keeper recalculates <c>maxHealth</c> from the area level every time the prefab is validated
        /// — so the number the author typed would survive until the first time anyone touched the
        /// asset, and then quietly become something else.
        /// </remarks>
        private static void ApplyHealth(GameObject root, Request request)
        {
            HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
            DimensionCreatureStatsTemplate stats = request.Stats;

            health.dontCalculateHealthFromLevel = stats.UsesAuthoredNumbers;

            if (stats.UsesAuthoredNumbers)
            {
                health.maxHealth = stats.MaxHealth;
                health.maxHealthMultiplier = 1f;
            }

            health.overrideStartHealth = stats.HasStartHealthOverride;
            health.normalizedOverrideStartHealth = stats.StartHealthFraction;
            health.startHealth = Mathf.RoundToInt(health.maxHealth * stats.StartHealthFraction);
        }

        private static void ApplyDamageReduction(GameObject root, Request request)
        {
            DimensionCreatureStatsTemplate stats = request.Stats;

            // A creature with no reduction and no caps wants no component at all — an empty one is a
            // row the game reads every hit to learn that nothing happens.
            bool wanted = stats.DamageReduction > 0 ||
                stats.MaxDamagePerHit > 0 ||
                stats.MinDamagePerHit > 0 ||
                !stats.UsesAuthoredNumbers;

            if (!wanted)
            {
                RemoveComponentIfPresent<DamageReductionAuthoring>(root);
                return;
            }

            DamageReductionAuthoring reduction = EnsureComponent<DamageReductionAuthoring>(root);
            reduction.calculateReductionFromLevel = !stats.UsesAuthoredNumbers;
            reduction.reduction = stats.DamageReduction;
            reduction.reductionMultiplier = 1f;
            reduction.maxDamagePerHit = stats.MaxDamagePerHit;
            reduction.minDamagePerHit = stats.MinDamagePerHit;
        }

        private static void ApplyMovement(GameObject root, Request request)
        {
            MovementSpeedAuthoring movement = EnsureComponent<MovementSpeedAuthoring>(root);
            movement.speed = request.Stats.MoveSpeed;
        }

        /// <summary>
        /// Applies only the chase values the author actually set.
        /// </summary>
        /// <remarks>
        /// Left-alone fields matter here. A borrowed behaviour ships with chase distances tuned to how
        /// its attack works, and writing zero over them would produce a creature that runs into the
        /// player and never swings. So an unset field keeps the behaviour's own value.
        /// </remarks>
        private static void ApplyChase(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            DimensionCreatureStatsTemplate stats = request.Stats;

            // An untouched pursuit section must not add a chase, and must not overwrite one a
            // behaviour brought with it. See DimensionPursuitTemplate.HasAnySetting.
            DimensionPursuitTemplate pursuit =
                request.Combat != null ? request.Combat.Pursuit : null;
            bool authoredPursuit = pursuit != null && pursuit.HasAnySetting;

            if (!stats.HasChaseAtDistance && !stats.HasChaseSpeedMultiplier && !authoredPursuit)
            {
                return;
            }

            ChaseStateAuthoring chase = EnsureComponent<ChaseStateAuthoring>(root);
            if (stats.HasChaseAtDistance)
            {
                chase.chaseAtDistance = stats.ChaseAtDistance;
            }

            if (stats.HasChaseSpeedMultiplier)
            {
                chase.moveSpeedMultiplier = stats.ChaseSpeedMultiplier;
            }

            if (!authoredPursuit)
            {
                return;
            }

            DimensionObjectSpine.ApplyPursuit(root, pursuit);

            if (pursuit.StandoffRangeIsBackwards)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' hangs back further than it will ever close in, " +
                    "so its standoff range is inside out. The two have been read as the same " +
                    "distance, which makes it stand still at that range instead.");
            }

            if (pursuit.WillFollowForeverWithoutArriving)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' needs a path before it will chase and also never " +
                    "gives up once it starts. A target that becomes unreachable will be followed " +
                    "forever by something that cannot arrive.");
            }
        }

        /// <summary>
        /// Gives the creature the state machine every vanilla creature has.
        /// </summary>
        /// <remarks>
        /// <c>StateAuthoring</c> is the root the other states hang off, and <c>IdleState</c> and
        /// <c>TookDamageState</c> sit beside it on 1,300+ vanilla prefabs — chests included. Without
        /// them a creature never returns to idle after acting and never reacts visibly to being hit.
        /// The generator wrote only <c>DeathState</c> before this.
        /// </remarks>
        private static void ApplyStateMachine(GameObject root, Request request)
        {
            DimensionObjectSpine.ApplyDamageableStates(root);

            // What every vanilla creature carries and ours did not. The area level is CONDITIONAL:
            // the attack components overwrite their damage from it in OnValidate with no opt-out, so
            // giving one to a creature whose author asked for exact numbers would make authored
            // attack damage impossible to keep.
            DimensionObjectSpine.ApplyUniversal(root, false);
            DimensionObjectSpine.ApplyAreaLevel(root, !request.Stats.UsesAuthoredNumbers);
        }

        /// <summary>
        /// How the creature perceives things and decides what is an enemy.
        /// </summary>
        /// <remarks>
        /// Chasing is downstream of noticing. <c>ChaseStateAuthoring</c> on its own has nothing to
        /// chase — <c>NearbyEntitiesTrackerAuthoring</c> is what populates the candidates, and
        /// <c>FactionAuthoring</c> plus <c>BehaviourTagsAuthoring</c> decide which of them count.
        /// Every vanilla enemy carries all three.
        /// </remarks>
        private static void ApplyPerception(
            GameObject root,
            DimensionCreatureCombatTemplate combat,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            // A LAYER MASK OF ZERO NOTICES NOTHING, EVER. NearbyEntitiesTrackerSystem hands the
            // number straight to the overlap as CollidesWith, so zero means the list of nearby
            // things stays empty for the creature's whole life — and everything that picks a target
            // off that list (melee, ranged, ray, mortar, eating, healing allies, placing objects,
            // and the chase's candidate list) finds nobody. The field's default is 0 and its label
            // says that leaves the game's own choice, so this is where that becomes true: the
            // game's own creatures watch one layer, and LarvaEntity is where the number comes from.
            uint watches = combat.NoticesThingsOnLayers > 0
                ? (uint)combat.NoticesThingsOnLayers
                : DimensionQueryCompanions.VanillaCreatureNoticeLayers;

            // A REACH OF ZERO IS THE SAME FAILURE AS A MASK OF ZERO, and it is allowed by the
            // field. An overlap of no size returns nothing whatever it is watching, so a creature
            // authored at zero notices nothing for its whole life — and the framework's own
            // "cannot see far enough to shoot" warning is itself gated on the reach being above
            // zero, so it says nothing either. The game's own creature reaches eight tiles.
            float reach = combat.DetectionRadius > 0f
                ? combat.DetectionRadius
                : DimensionQueryCompanions.VanillaCreatureNoticeRadius;

            if (combat.DetectionRadius <= 0f)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' was set to notice things no distance away, " +
                    "which means it notices nothing at all — not a player to chase, not a meal to " +
                    "eat, not an ally to heal. It was generated noticing things " +
                    DimensionQueryCompanions.VanillaCreatureNoticeRadius +
                    " tiles away, which is what the game's own creatures do.");
            }

            DimensionQueryCompanions.SeesNearbyThings(
                root,
                reach,
                watches,
                combat.ChecksForThingsEveryFrame,
                delegate(string message)
                {
                    report.Warnings.Add("'" + request.DisplayName + "' " + message);
                });

            CombatRadiusAuthoring combatRadius = EnsureComponent<CombatRadiusAuthoring>(root);
            combatRadius.radius = combat.CombatRadius;

            // Conditions are how Core Keeper does poison, slow, burning and every buff — a creature
            // without this component simply ignores all of them, which reads as immunity nobody asked
            // for.
            EnsureComponent<SupportsConditionsAuthoring>(root);

            FactionID faction;
            if (!string.IsNullOrEmpty(combat.FactionId) &&
                Enum.TryParse(combat.FactionId, false, out faction))
            {
                EnsureComponent<FactionAuthoring>(root).faction = faction;
            }
            else if (!string.IsNullOrEmpty(combat.FactionId))
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' names faction '" + combat.FactionId +
                    "', which the game does not have. It will have no faction, so nothing will treat " +
                    "it as an enemy.");
            }

            string[] wants = combat.WantsToAttackTags;
            string[] cant = combat.CantAttackTags;

            // IT USED TO TAKE THE COMPONENT OFF when the temper was Custom and both lists were
            // empty, and that quietly switched the whole creature off. Every attack, the chase,
            // eating and exploding are gated on the tag component being PRESENT —
            // ChaseStateRequest, MeleeAttackStateRequest, RangeAttackStateRequest,
            // ChargeAttackStateRequest, JumpAttackStateRequest, EatStateRequest and
            // ExplodeStateRequest all ask for it before they look at anything else, and six systems
            // name it in their work query. Empty lists are a real answer ("nothing here is my
            // enemy"); no component at all is not an answer, it is the creature leaving the game.
            // Clearing the lists is what makes generation authoritative, and that still happens.

            BehaviourTagsAuthoring tags = DimensionQueryCompanions.DecidesWhoIsAnEnemy(root);
            tags.wantsToAttackTags = ParseTags(wants, request, report);
            tags.cantAttackTags = ParseTags(cant, request, report);

            // The third list. It used to be forced empty here with no field behind it, which made
            // BehaviourTagsCD.Eats false for every creature this framework has ever made — so the
            // eat, breed and evolve answers all generated cleanly and none of them could fire.
            tags.eatsTags = ParseTags(combat.EatsTags, request, report);
        }

        private static List<ObjectCategoryTag> ParseTags(
            string[] names,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            List<ObjectCategoryTag> parsed = new List<ObjectCategoryTag>();
            for (int i = 0; i < names.Length; i++)
            {
                ObjectCategoryTag tag;
                if (Enum.TryParse(names[i], false, out tag))
                {
                    parsed.Add(tag);
                }
                else
                {
                    report.Warnings.Add(
                        "'" + request.DisplayName + "' names category '" + names[i] +
                        "', which the game does not have. That part of the rule was left out.");
                }
            }

            return parsed;
        }

        /// <summary>The two categories every hostile creature in the game goes after.</summary>
        /// <remarks>
        /// Read straight off the game's own prefabs: Larva and Caveling both author
        /// <c>wantsToAttackTags = [Player, HostileCreature]</c> and nothing else. HostileCreature
        /// is what makes a charmed pet, a minion and a rival faction's mob fightable — leaving it
        /// out would produce a creature that hunts players and stands next to a summoned ally.
        /// </remarks>
        private static readonly ObjectCategoryTag[] FightsPlayersAndMonsters =
        {
            ObjectCategoryTag.Player,
            ObjectCategoryTag.HostileCreature
        };

        /// <summary>
        /// Turns the chosen temperament into the components that decide whether it fights.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This runs AFTER <see cref="ApplyPerception"/> and <see cref="ApplyChase"/> because it
        /// overwrites what both of them wrote. The temperament is the plain-language question; the
        /// faction and category tags under Attacks are the expert one, and an author who wants to
        /// answer the expert question chooses Custom, which leaves both attack tag lists exactly
        /// as typed.
        /// </para>
        /// <para>
        /// THREE vanilla levers, not two. Measured across all 83 of the game's creature prefabs
        /// that carry a chase, plus the code that reads them:
        /// </para>
        /// <para>
        /// 1. <c>wantsToAttackTags</c> gates chase-target selection —
        /// <c>ChaseStateRequest</c> will not pick ANY target that fails
        /// <c>WantsToAndCanAttack</c> (`ck-db\Pug.Other\ChaseStateRequest.cs:216`), and that check
        /// runs for the last attacker too. Every hostile prefab in the game authors exactly
        /// <c>[Player, HostileCreature]</c>; all twelve cattle prefabs author an EMPTY list, which
        /// is what makes a cow a cow.
        /// </para>
        /// <para>
        /// 2. <c>cantAttackTags</c> gates the swing itself. <c>MeleeAttackStateRequest</c> consults
        /// <c>WantsToAttack</c> only while the creature is already chasing a DIFFERENT target
        /// (`MeleeAttackStateRequest.cs:136-139`); otherwise only the cannot-list stands between it
        /// and anything adjacent. <c>RangeAttackStateRequest.cs:160</c> is the same shape. Vanilla
        /// cattle leave this empty and get away with it only because they carry no attack at all;
        /// a Passive creature the author gave an attack needs the list filled or it will still hit
        /// whoever brushes past it.
        /// </para>
        /// <para>
        /// 3. The creature's OWN <c>Cattle</c> category tag is what marks it harmless to everything
        /// else. Every one of the twelve cattle prefabs tags itself
        /// <c>HostileCreature + Cattle</c>; every hostile tags itself <c>HostileCreature</c> alone.
        /// <c>Cattle</c> is not decoration: all ten pet prefabs and all six minion prefabs list it
        /// in their own <c>cantAttackTags</c>, and the player's "kill everything" command skips any
        /// entity carrying it (`ck-db\Pug.Other\PlayerCommand\ServerSystem.cs:235`). Without it a
        /// Passive animal is butchered by the first tamed pet that walks past, which is not what
        /// anybody means by passive.
        /// </para>
        /// <para>
        /// Defensive's own lever is the fourth, and it is NOT written here — see
        /// <see cref="Creatures.DimensionHoldsFireAuthoring"/> for why zeroing the authored chase
        /// distance breaks pathfinding, and what is done instead.
        /// </para>
        /// <para>
        /// Hostile and Defensive both ENSURE a chase exists, because both of them promise pursuit
        /// and neither can keep that promise without one. They only ever add a chase where there is
        /// none: running last means a borrowed behaviour's own tuning is already on the object and
        /// is never overwritten. Passive never touches the chase — a cow walks to its feed trough
        /// with it.
        /// </para>
        /// </remarks>
        private static void ApplyTemperament(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            bool passive = request.Aggression == DimensionSpawnAggressionKind.Passive;
            bool defensive = request.Aggression == DimensionSpawnAggressionKind.Defensive;

            // Cleanup that has to run for EVERY temper, Custom included. A prefab regenerated
            // after its temper changed must not keep the marker or the identity tag the previous
            // temper left on it — a creature that is no longer Defensive but still holds its fire
            // is exactly the kind of stale prefab nobody thinks to look for.
            if (defensive)
            {
                EnsureComponent<Creatures.DimensionHoldsFireAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<Creatures.DimensionHoldsFireAuthoring>(root);
            }

            // Cattle is an identity tag the generator owns, not one of the two attack lists an
            // author types, so Custom gets it cleared like everything else rather than inheriting
            // a harmlessness it never asked for.
            DimensionObjectSpine.SetCategoryTag(root, ObjectCategoryTag.Cattle, passive);

            if (request.Aggression == DimensionSpawnAggressionKind.Custom)
            {
                WarnAboutCustomThatCannotFight(root, request, report);
                return;
            }

            BehaviourTagsAuthoring tags = EnsureComponent<BehaviourTagsAuthoring>(root);
            if (tags.wantsToAttackTags == null)
            {
                tags.wantsToAttackTags = new List<ObjectCategoryTag>();
            }

            if (tags.cantAttackTags == null)
            {
                tags.cantAttackTags = new List<ObjectCategoryTag>();
            }

            if (tags.eatsTags == null)
            {
                tags.eatsTags = new List<ObjectCategoryTag>();
            }

            for (int i = 0; i < FightsPlayersAndMonsters.Length; i++)
            {
                ObjectCategoryTag tag = FightsPlayersAndMonsters[i];
                SetTag(tags.wantsToAttackTags, tag, !passive);
                SetTag(tags.cantAttackTags, tag, passive);
            }

            switch (request.Aggression)
            {
                case DimensionSpawnAggressionKind.Passive:
                    WarnAboutAPassiveFighter(root, request, report);
                    break;

                case DimensionSpawnAggressionKind.Defensive:
                    EnsureAChaseToPursueWith(root, request, report, "Defensive");
                    WarnAboutADefenderThatCannotHitBack(root, request, report);
                    break;

                case DimensionSpawnAggressionKind.Hostile:
                    EnsureAChaseToPursueWith(root, request, report, "Hostile");
                    break;
            }
        }

        /// <summary>
        /// Gives a temper that promises pursuit something to pursue with.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A creature with no <c>ChaseStateAuthoring</c> has no chase state, and without a chase
        /// state <c>ChaseStateRequest.ShouldUpdate</c> returns false for it forever — it can only
        /// ever swing at whatever is already touching it. Both Hostile ("hunts players down") and
        /// Defensive ("comes after whoever hit it") are claims about following someone, so both
        /// need one. Before this the generator only WARNED, which left the two most-used tempers
        /// producing a creature that stands still.
        /// </para>
        /// <para>
        /// How far it can see is the right distance to borrow: it is the only pursuit number the
        /// author has already given, and it is what <c>NearbyEntitiesTrackerAuthoring</c> was set
        /// to, so the creature cannot be told to chase further than it can notice. On a Defensive
        /// creature the distance never gates aggro at all — the runtime marker zeroes that — but it
        /// still sizes the path search, so it must be a real number rather than zero.
        /// </para>
        /// <para>
        /// It never overwrites a distance that is already there. Running last is what makes that
        /// safe: a borrowed behaviour ships chase distances tuned to how its attack works, and they
        /// are already on the object by the time this sees it.
        /// </para>
        /// </remarks>
        private static ChaseStateAuthoring EnsureAChaseToPursueWith(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report,
            string temperName)
        {
            float sightRadius = request.Combat != null ? request.Combat.DetectionRadius : 0f;
            ChaseStateAuthoring chase = root.GetComponent<ChaseStateAuthoring>();

            if (chase != null && chase.chaseAtDistance > 0f)
            {
                return chase;
            }

            if (sightRadius <= 0f)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' is " + temperName + " but cannot see anything: " +
                    "its detection radius is zero and no chase distance was given, so it will " +
                    "never follow anyone and can only hit whatever is already within reach of " +
                    "its attack. Give it a detection radius under Stats.");
                return chase;
            }

            if (chase == null)
            {
                chase = EnsureComponent<ChaseStateAuthoring>(root);

                // Matching what the game's own creatures carry. Left at zero a chase enters the
                // state and then stands there, because the speed it moves at is this multiplied
                // by the creature's movement speed (`ChaseStateUtility.CalculateMovementSpeed`).
                chase.moveSpeedMultiplier = 1f;
            }

            chase.chaseAtDistance = sightRadius;
            return chase;
        }

        /// <summary>Says so when Custom has left a creature unable to pick a target at all.</summary>
        /// <remarks>
        /// The expert escape hatch is worth keeping, but its most likely outcome is silence: an
        /// author who chooses Custom and types nothing gets an empty <c>wantsToAttackTags</c>, and
        /// an empty wants list fails <c>WantsToAndCanAttack</c> for everything, so no chase target
        /// is ever picked. The field's own tooltip used to promise the faction would carry it,
        /// which is false — the faction check runs in ADDITION to the tag check
        /// (`ChaseStateRequest.cs:216`), never instead of it.
        /// </remarks>
        private static void WarnAboutCustomThatCannotFight(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            BehaviourTagsAuthoring tags = root.GetComponent<BehaviourTagsAuthoring>();
            bool wantsSomething = tags != null && tags.wantsToAttackTags != null &&
                                  tags.wantsToAttackTags.Count > 0;
            if (wantsSomething)
            {
                return;
            }

            bool armed = root.GetComponent<MeleeAttackStateAuthoring>() != null ||
                         root.GetComponent<RangeAttackStateAuthoring>() != null;
            if (!armed)
            {
                return;
            }

            report.Warnings.Add(
                "'" + request.DisplayName + "' has its temperament set to Custom and no attack " +
                "categories typed, so it will never choose anyone to chase — its faction cannot " +
                "carry that on its own. Type the categories it should go after under Attacks, or " +
                "choose Hostile, Defensive or Passive instead.");
        }

        /// <summary>Says so when a passive creature was given an attack it can never use.</summary>
        private static void WarnAboutAPassiveFighter(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            if (root.GetComponent<MeleeAttackStateAuthoring>() == null &&
                root.GetComponent<RangeAttackStateAuthoring>() == null)
            {
                return;
            }

            report.Warnings.Add(
                "'" + request.DisplayName + "' is Passive and also has an attack, which it will " +
                "never use — Passive tells it it may not hit players or other creatures. Set it " +
                "to Defensive if it should fight back when something hits it.");
        }

        /// <summary>Says so when a defensive creature has nothing to defend itself with.</summary>
        /// <remarks>
        /// <para>
        /// Being unarmed is the only way left to be a toothless defender, and it is the one an
        /// author cannot see in the prefab. A Defensive creature with no attack is a Passive one
        /// that has been told to retaliate: it acquires the person who hit it, walks to them, and
        /// stands there.
        /// </para>
        /// <para>
        /// Having no chase is no longer one of the ways, because <see cref="ApplyTemperament"/>
        /// now gives a defender one. Neither is pathfinding: the earlier implementation baked the
        /// chase distance to zero, and <c>PathFindingConversion.CreatePathfindingEntity</c> sizes
        /// the path search from that same number, so a defender that insisted on a path got a
        /// zero-radius search and could never move. That is exactly why the zero moved off the
        /// prefab and onto <see cref="Creatures.DimensionHoldsFireSystem"/>, which writes only
        /// <c>ChaseStateCD.chaseAtDistanceSq</c> and leaves the path search its real radius.
        /// Pathfinding and Defensive are now a legal combination, so nothing is said about it.
        /// </para>
        /// </remarks>
        private static void WarnAboutADefenderThatCannotHitBack(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            if (root.GetComponent<MeleeAttackStateAuthoring>() == null &&
                root.GetComponent<RangeAttackStateAuthoring>() == null)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' is Defensive but has no attack, so nothing it " +
                    "does when hit will reach anyone. Give it an attack under Attacks, or set it " +
                    "to Passive.");
            }

            // Nothing is said here about a missing chase. The only way to reach this without one
            // is a detection radius of zero, and EnsureAChaseToPursueWith has already said exactly
            // that, naming the same fix — two warnings for one cause teaches authors to skim them.

            // The whole temper hangs off LastAttackerCD, and EnemyConverter is the only thing that
            // adds it. The player's own attack path only ever SETS that component where it already
            // exists — `ecb.SetComponent<LastAttackerCD>` guarded by `HasComponent` at
            // `ck-db\Pug.Other\EntityUtility.cs:843` — so a creature without the enemy tag never
            // learns who hit it and can never answer. Nothing in the studio can currently turn the
            // tag off, but this is the one combination that would fail completely and silently.
            if (!request.IsEnemy)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' is Defensive but does not carry the game's " +
                    "enemy tag, so it never records who hit it and will never answer anyone. " +
                    "Defensive needs that tag; set it to Passive instead.");
            }
        }

        /// <summary>Adds or removes one category tag, without ever listing it twice.</summary>
        private static void SetTag(List<ObjectCategoryTag> tags, ObjectCategoryTag tag, bool present)
        {
            bool has = tags.Contains(tag);
            if (present && !has)
            {
                tags.Add(tag);
            }
            else if (!present && has)
            {
                tags.RemoveAll(delegate(ObjectCategoryTag t) { return t == tag; });
            }
        }

        /// <summary>
        /// What it does when nothing is happening.
        /// </summary>
        /// <remarks>
        /// <c>RandomWalkState</c>, not <c>RoamingState</c> — measured across the game's 115 enemy
        /// prefabs, random walk is what they wander with and roaming state is on four prefabs total.
        /// Removed rather than left behind when the answer turns to "stand still", or the creature
        /// keeps wandering with nothing in the asset still asking it to.
        /// </remarks>
        private static void ApplyIdleMovement(GameObject root, DimensionCreatureCombatTemplate combat)
        {
            if (!combat.WandersWhenIdle)
            {
                RemoveComponentIfPresent<RandomWalkStateAuthoring>(root);
                return;
            }

            RandomWalkStateAuthoring walk = EnsureComponent<RandomWalkStateAuthoring>(root);
            walk.minWalkDistance = combat.MinWanderDistance;
            walk.maxWalkDistance = combat.MaxWanderDistance;
            walk.minIdleDuration = combat.MinWanderPause;
            walk.maxIdleDuration = combat.MaxWanderPause;
            walk.movementSpeedMultiplier = combat.WanderSpeedMultiplier;
            walk.maxWalkDuration = combat.MaxWanderDuration;
            walk.walkPatternBehaviourDefinition = combat.WalkPattern;
            walk.overrideUseAuthoringBehaviourValuesForAllPatternBaseMovementProperties =
                combat.OwnNumbersBeatTheWalkPattern;
        }

        /// <summary>
        /// The attack itself — the piece that was missing entirely.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Before this, a generated creature got <c>ChaseStateAuthoring</c> and nothing else: it ran
        /// at the player and then stood there, because Core Keeper puts the attack on a separate
        /// component and neither <c>MeleeAttackStateAuthoring</c> nor <c>RangeAttackStateAuthoring</c>
        /// was being written.
        /// </para>
        /// <para>
        /// Damage left at zero is deliberately NOT written, so the game's own level scaling stays in
        /// charge — <c>MeleeAttackStateAuthoring.OnValidate</c> recomputes <c>meleeDamage</c> from
        /// <c>AreaLevelAuthoring</c> whenever one is present, so writing a number there would be
        /// overwritten anyway on the next validate and is worse than leaving it alone.
        /// </para>
        /// </remarks>
        private static void ApplyAttacks(
            GameObject root,
            DimensionCreatureCombatTemplate combat,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            if (combat.HasMelee)
            {
                // Melee needs to know it actually touched something.
                EnsureComponent<DetectCollisionAuthoring>(root);

                MeleeAttackStateAuthoring melee = EnsureComponent<MeleeAttackStateAuthoring>(root);
                melee.anticipationDuration = combat.MeleeWindUp;
                melee.hitDuration = combat.MeleeSwingDuration;
                melee.minCooldown = combat.MeleeMinCooldown;
                melee.maxCooldown = combat.MeleeMaxCooldown;
                melee.minDistanceToAttemptHit = combat.MeleeReach;
                melee.hitDistanceInfront = combat.MeleeSwingLandsAhead;
                melee.hitRadius = combat.MeleeHitRadius;
                melee.amountOfHits = combat.MeleeHits;
                melee.pushForce = combat.MeleePushForce;
                melee.hitTiles = combat.MeleeBreaksTiles;
                melee.tileDamage = combat.MeleeTileDamage;

                if (!combat.MeleeDamageFromLevel)
                {
                    melee.meleeDamage = combat.MeleeDamage;
                }

                // The half of the swing that decides how it feels — and the multipliers, which are
                // the only melee damage dial that survives a tier at all.
                DimensionObjectSpine.ApplyMeleeShape(
                    root,
                    combat.MeleeShape,
                    delegate(string objectId) { return ResolveObject(objectId); },
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    });

                if (combat.MeleeDamageFromLevel && combat.MeleeShape.MultipliedDownToNoDamage)
                {
                    report.Warnings.Add(
                        "'" + request.DisplayName + "' takes its melee damage from its tier and " +
                        "then multiplies it by zero, so it swings and hurts nothing. Its damage " +
                        "number is not read on a tiered creature; the multiplier is.");
                }
            }
            else
            {
                RemoveComponentIfPresent<MeleeAttackStateAuthoring>(root);
            }

            if (!combat.HasRanged)
            {
                RemoveComponentIfPresent<RangeAttackStateAuthoring>(root);
                return;
            }

            RangeAttackStateAuthoring ranged = EnsureComponent<RangeAttackStateAuthoring>(root);
            ranged.anticipationDuration = combat.RangedWindUp;
            ranged.minCooldown = combat.RangedMinCooldown;
            ranged.maxCooldown = combat.RangedMaxCooldown;
            ranged.minDistanceFromTargetToAllowAttack = combat.RangedMinDistance;
            ranged.maxDistanceFromTargetToAllowAttack = combat.RangedMaxDistance;
            ranged.projectilesPerShot = combat.ProjectilesPerShot;
            ranged.spreadAngle = combat.SpreadAngle;
            ranged.timeBetweenShots = combat.TimeBetweenShots;

            if (!combat.RangedDamageFromLevel)
            {
                ranged.rangeDamage = combat.RangedDamage;
            }

            // The shot pattern, and the multiplier that is the only ranged damage a tier keeps.
            DimensionObjectSpine.ApplyRangedShape(
                root,
                combat.RangedShape,
                delegate(string message)
                {
                    report.Warnings.Add("'" + request.DisplayName + "' " + message);
                });

            if (combat.RangedDamageFromLevel && combat.RangedShape.MultipliedDownToNoDamage)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' takes its ranged damage from its tier and then " +
                    "multiplies it by zero, so it fires and hurts nothing. Its damage number is not " +
                    "read on a tiered creature; the multiplier is.");
            }

            ObjectID projectile = ResolveObject(combat.ProjectileItemId);

            // WRITTEN EVERY TIME, the None included. For one of the mod's own projectiles the None
            // is the placeholder the link hydration overwrites at load — a creature's shot is
            // RangeAttackStateCD.projectileID, which is a different component from a bow's, and
            // this is the field behind DimensionObjectLink.CreatureShot. Writing it also stops a
            // shot changed to a typo from quietly staying the old projectile, because generation
            // reloads the existing prefab.
            ranged.projectileID = projectile;
            if (projectile == ObjectID.None &&
                !IsDeferred(combat.ProjectileItemId) &&
                !string.IsNullOrEmpty(combat.ProjectileItemId))
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' shoots '" + combat.ProjectileItemId +
                    "', which is neither one of this mod's projectiles nor one the game has. It " +
                    "will go through the motions of shooting and produce nothing.");
            }
        }

        /// <summary>
        /// Writes each ability onto the component that implements it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Every kind is removed first and then re-added only if the author asked for it, so dropping
        /// an ability from the list actually takes it off the creature. Left as add-only, a boss would
        /// accumulate every ability it had ever been given across edits, with nothing in the asset
        /// still describing them.
        /// </para>
        /// <para>
        /// Powers left at zero are not written, for the same reason the main attacks leave them alone:
        /// these components recompute damage from <c>AreaLevelAuthoring</c> in <c>OnValidate</c>.
        /// </para>
        /// </remarks>
        private static void ApplyAbilities(
            GameObject root,
            DimensionCreatureCombatTemplate combat,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            RemoveComponentIfPresent<ChargeAttackStateAuthoring>(root);
            RemoveComponentIfPresent<JumpAttackStateAuthoring>(root);
            RemoveComponentIfPresent<ShootMortarProjectileStateAuthoring>(root);
            RemoveComponentIfPresent<ExplodeStateAuthoring>(root);
            RemoveComponentIfPresent<TeleportStateAuthoring>(root);
            RemoveComponentIfPresent<EnrageStateAuthoring>(root);
            RemoveComponentIfPresent<SleepStateAuthoring>(root);
            RemoveComponentIfPresent<EatStateAuthoring>(root);
            RemoveComponentIfPresent<BreedStateAuthoring>(root);
            RemoveComponentIfPresent<EvolveStateAuthoring>(root);
            RemoveComponentIfPresent<HealOtherEntityStateAuthoring>(root);
            RemoveComponentIfPresent<VulnerableStateAuthoring>(root);

            DimensionCreatureAbility[] abilities = combat.Abilities;
            for (int i = 0; i < abilities.Length; i++)
            {
                ApplyAbility(root, abilities[i], request, report);
            }

            if (combat.HasDuplicateAbilities)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' lists the same ability more than once. Each one is " +
                    "a single component, so the later entry overwrites the earlier and its settings " +
                    "are lost.");
            }
        }

        private static void ApplyAbility(
            GameObject root,
            DimensionCreatureAbility ability,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            if (ability.IsMissingTargetObject)
            {
                report.Warnings.Add(
                    "'" + request.DisplayName + "' has a " + ability.Kind +
                    " ability with nothing named for it to use, so it will run and produce nothing.");
            }

            ObjectID target = ResolveObject(ability.TargetObjectId);
            if (target == ObjectID.None && !string.IsNullOrEmpty(ability.TargetObjectId))
            {
                // Two different problems, and telling a creator the wrong one costs them an hour.
                // An ability's target is the one reference on a creature the framework still cannot
                // hand over to the runtime, so one of the mod's own objects here is a real limit
                // rather than a mistake, and it is said as one.
                report.Warnings.Add(IsDeferred(ability.TargetObjectId)
                    ? "'" + request.DisplayName + "' has a " + ability.Kind + " ability naming '" +
                        ability.TargetObjectId + "', which is one of your own. An ability cannot " +
                        "point at your own objects yet — only at the game's. Name one of the " +
                        "game's, or give the creature this behaviour through its own attack instead."
                    : "'" + request.DisplayName + "' has a " + ability.Kind + " ability naming '" +
                        ability.TargetObjectId + "', which is neither one of this mod's objects " +
                        "nor one the game has, so that ability produces nothing.");
            }

            switch (ability.Kind)
            {
                case DimensionCreatureAbilityKind.ChargeAttack:
                {
                    ChargeAttackStateAuthoring charge = EnsureComponent<ChargeAttackStateAuthoring>(root);
                    charge.anticipationDuration = ability.WindUp;
                    charge.chargeDuration = ability.Duration;
                    charge.minCooldown = ability.MinCooldown;
                    charge.maxCooldown = ability.MaxCooldown;
                    charge.distanceToProvokeCharge = ability.Range;
                    charge.moveSpeedMultiplier = ability.SpeedMultiplier;

                    if (!ability.PowerFromLevel)
                    {
                        charge.damage = ability.Power;
                    }

                    DimensionObjectSpine.ApplyChargeShape(
                        root,
                        ability.ChargeShape,
                        delegate(string message)
                        {
                            report.Warnings.Add("'" + request.DisplayName + "' " + message);
                        });
                    break;
                }

                case DimensionCreatureAbilityKind.JumpAttack:
                {
                    JumpAttackStateAuthoring jump = EnsureComponent<JumpAttackStateAuthoring>(root);
                    jump.anticipationTime = ability.WindUp;
                    jump.airTime = ability.Duration;
                    jump.minCooldown = ability.MinCooldown;
                    jump.maxCooldown = ability.MaxCooldown;
                    jump.distanceToAttack = ability.Range;
                    jump.jumpMoveSpeed = ability.SpeedMultiplier;
                    jump.jumpDamageMultiplier = ability.LeapHitsThisHardForItsTier;
                    jump.canOnlyAttackEnemiesAndPlayer = ability.LeapOnlyHitsEnemiesAndPlayers;
                    if (!ability.PowerFromLevel)
                    {
                        jump.jumpDamage = ability.Power;
                    }

                    break;
                }

                case DimensionCreatureAbilityKind.MortarShot:
                {
                    ShootMortarProjectileStateAuthoring mortar =
                        EnsureComponent<ShootMortarProjectileStateAuthoring>(root);
                    mortar.anticipationDuration = ability.WindUp;
                    mortar.attackDuration = ability.Duration;
                    mortar.minCooldown = ability.MinCooldown;
                    mortar.maxCooldown = ability.MaxCooldown;
                    mortar.maxDistanceToTargetToShoot = ability.Range;
                    if (target != ObjectID.None)
                    {
                        mortar.mortarProjectileID = target;
                    }

                    if (!ability.PowerFromLevel)
                    {
                        mortar.mortarDamage = ability.Power;
                    }

                    DimensionObjectSpine.ApplyMortarBarrage(
                        root,
                        ability.MortarBarrage,
                        delegate(string message)
                        {
                            report.Warnings.Add("'" + request.DisplayName + "' " + message);
                        });
                    break;
                }

                case DimensionCreatureAbilityKind.Explode:
                {
                    ExplodeStateAuthoring explode = EnsureComponent<ExplodeStateAuthoring>(root);
                    explode.explodeDuration = ability.Duration;
                    explode.distanceToExplode = ability.Range;
                    explode.minHealthRatioToExplode = ability.HealthFraction;
                    explode.explodeOnDeath = ability.OnDeath;

                    // Vanilla's own default is true, which makes anything carrying this component blow
                    // up the moment it spawns. That is right for a thrown bomb and catastrophic for a
                    // creature, so it is off unless the ability is explicitly a death rattle.
                    explode.explodeOnInitialization = false;
                    if (!ability.PowerFromLevel)
                    {
                        explode.damage = ability.Power;
                    }

                    // ONLY THE EXPLOSION OBJECT DEPENDS ON HAVING ONE. The five settings below
                    // were inside this branch, which meant a creature that blew up without naming
                    // an explosion silently lost its blast variation, its terrain damage, both of
                    // its multipliers and whether it drops its loot — five controls a creator can
                    // see and fill in, thrown away for an unrelated reason. They describe the
                    // blast itself and are written whether or not an explosion object is named.
                    if (target != ObjectID.None)
                    {
                        explode.explosionID = target;
                    }

                    explode.explosionVariation = ability.ExplosionVariation;
                    explode.dropLootOnDestroy = ability.DropsItsLootWhenItBlowsUp;
                    explode.tileDamage = ability.FlatTerrainDamage;
                    explode.damageMultiplier = ability.HitsThisHardForItsTier;
                    explode.tileDamageMultiplier = ability.BreaksTerrainThisHardForItsTier;

                    break;
                }

                case DimensionCreatureAbilityKind.Teleport:
                {
                    TeleportStateAuthoring teleport = EnsureComponent<TeleportStateAuthoring>(root);
                    teleport.startTeleportDuration = ability.WindUp;
                    teleport.endTeleportDuration = ability.Duration;
                    teleport.minCooldown = ability.MinCooldown;
                    teleport.maxCooldown = ability.MaxCooldown;
                    teleport.maxTeleportDistanceFromPlayer = ability.Range;
                    teleport.canOnlyTeleportToNonBlockedGround = true;
                    teleport.canOnlyTeleportBackToSpawn = ability.OnlyGoesBackToWhereItSpawned;
                    teleport.canTeleportToPitAndWater = ability.CanLandOnPitsAndWater;
                    teleport.allowedRadiusToMoveFromPosition =
                        ability.StaysWithinThisFarOfWhereItWas;
                    teleport.minTeleportDistanceFromPlayer =
                        ability.NeverLandsCloserToThePlayerThan;
                    teleport.updateTilesAtAreaMinCorner = new Unity.Mathematics.int2(
                        ability.RefreshesTilesFromCorner.x,
                        ability.RefreshesTilesFromCorner.y);
                    teleport.updateTilesAtAreaMaxCorner = new Unity.Mathematics.int2(
                        ability.RefreshesTilesToCorner.x,
                        ability.RefreshesTilesToCorner.y);
                    break;
                }

                case DimensionCreatureAbilityKind.Enrage:
                {
                    EnrageStateAuthoring enrage = EnsureComponent<EnrageStateAuthoring>(root);
                    enrage.enrageAtHealthRatio = ability.HealthFraction;
                    enrage.duration = ability.Duration;
                    enrage.leaveEnrageAtHealthRatio = ability.CalmsDownAboveHealth;
                    break;
                }

                case DimensionCreatureAbilityKind.Sleep:
                {
                    SleepStateAuthoring sleep = EnsureComponent<SleepStateAuthoring>(root);
                    sleep.minPreFallAsleepDuration = ability.WindUp;
                    sleep.maxPreFallAsleepDuration = ability.WindUp;
                    sleep.minSleepDuration = ability.Duration;
                    sleep.maxSleepDuration = ability.Duration;
                    sleep.minSleepCooldown = ability.MinCooldown;
                    sleep.maxSleepCooldown = ability.MaxCooldown;
                    sleep.radiusFromVisiblePlayerToAwake = ability.Range;
                    sleep.wakeUpDuration = ability.WakeUpSeconds;
                    sleep.minRadiusFromOwnerToWakeUp = ability.WakesWhenItsOwnerIsWithin;
                    sleep.stayAwakeUntilNoVisiblePlayer = ability.StaysAwakeWhileSeen;
                    sleep.triggerAwakeOnClientWhenDamagingEntity =
                        ability.WakesTheMomentItHitsSomething;
                    break;
                }

                case DimensionCreatureAbilityKind.Eat:
                {
                    EatStateAuthoring eat = EnsureComponent<EatStateAuthoring>(root);
                    eat.duration = ability.Duration;
                    eat.distanceToEat = ability.Range;
                    eat.maxFoodUntilFull = ability.Amount;
                    eat.eatPostDuration = ability.PauseAfterEating;
                    break;
                }

                case DimensionCreatureAbilityKind.Breed:
                {
                    BreedStateAuthoring breed = EnsureComponent<BreedStateAuthoring>(root);
                    breed.mealsToTrigger = ability.Amount;
                    breed.minDistanceToBreed = ability.Range;
                    if (target != ObjectID.None)
                    {
                        breed.babyType = target;
                    }

                    // OUTSIDE THE BABY-TYPE BRANCH, and it used to be inside it. A mutation is a
                    // VARIATION of whatever the baby is — BreedStateConverter reads
                    // mutationChance and the weights whether or not babyType was set, and an
                    // unset babyType means the young are the same object as the parent, which is
                    // the ordinary case for an animal that breeds true. So an author who filled in
                    // mutation weights and left the baby type blank got a breeding animal with
                    // every mutation silently dropped.
                    breed.mutationChance = ability.MutationChance;
                    breed.mutationWeights =
                        new System.Collections.Generic.List<BreedStateAuthoring.VariationWithWeight>();
                    DimensionMutationWeight[] mutations = ability.Mutations;
                    for (int m = 0; m < mutations.Length; m++)
                    {
                        breed.mutationWeights.Add(new BreedStateAuthoring.VariationWithWeight
                        {
                            variation = mutations[m].Variation,
                            weight = mutations[m].Weight
                        });
                    }

                    if (ability.MutatesIntoNothing)
                    {
                        report.Warnings.Add(
                            "'" + request.DisplayName + "' can produce mutated young with no " +
                            "list of what they mutate into, so every baby comes out the same.");
                    }

                    break;
                }

                case DimensionCreatureAbilityKind.Evolve:
                {
                    EvolveStateAuthoring evolve = EnsureComponent<EvolveStateAuthoring>(root);
                    evolve.foodAmountToEvolve = ability.Amount;
                    if (target != ObjectID.None)
                    {
                        evolve.toEvolveInto = target;
                    }

                    // A count of meals to evolve after. The game decides whether a creature is
                    // ready to grow up by reading how many meals it remembers, and the evolve
                    // answer does not bring that record with it — so a creature told to evolve
                    // after three meals, and not separately told to remember its meals, was never
                    // considered for evolving at all. Both of the game's baby animals carry it.
                    if (root.GetComponent<MealsEatenAuthoring>() == null)
                    {
                        EnsureComponent<MealsEatenAuthoring>(root);
                        report.Warnings.Add(
                            "'" + request.DisplayName + "' grows up after a number of meals but " +
                            "was not set to remember its meals, and the game counts them off that " +
                            "record. It was generated remembering them, which is what the game's " +
                            "own young animals do.");
                    }

                    break;
                }

                case DimensionCreatureAbilityKind.HealAllies:
                {
                    HealOtherEntityStateAuthoring heal = EnsureComponent<HealOtherEntityStateAuthoring>(root);
                    heal.anticipationDuration = ability.WindUp;
                    heal.healDuration = ability.Duration;
                    heal.minCooldown = ability.MinCooldown;
                    heal.maxCooldown = ability.MaxCooldown;
                    heal.maxReachDistance = ability.Range;
                    if (!ability.PowerFromLevel)
                    {
                        heal.healPerSecond = ability.Power;
                        heal.donCalculateHealingFromLevel = true;
                    }

                    // OUTSIDE THE FIXED-POWER BRANCH, and all four used to be inside it. Read off
                    // HealOtherEntityStateConverter: healMultiplier is the field the LEVEL path
                    // uses (LevelToHealing(level, multiplier)), so it was the one field that could
                    // only ever matter to a healer scaled by its tier and it was written only for
                    // one that was not. The other three — heals a share of max health, keeps going
                    // until hit so many times, heals what it cannot see — are copied straight into
                    // HealOtherEntityStateCD whichever way the power is worked out.
                    heal.healMultiplier = ability.HealsThisMuchForItsTier;
                    heal.healPercentageOfHp = ability.HealsAShareOfMaxHealth;
                    heal.keepHealingUntilTakingDamageXTimes =
                        ability.KeepsHealingUntilHitThisManyTimes;
                    heal.skipVisibilityCheck = ability.HealsWhatItCannotSee;

                    break;
                }

                case DimensionCreatureAbilityKind.Vulnerable:
                {
                    VulnerableStateAuthoring vulnerable = EnsureComponent<VulnerableStateAuthoring>(root);
                    vulnerable.anticipationDuration = ability.WindUp;
                    vulnerable.vulnerableDuration = ability.Duration;
                    vulnerable.preAnticipationDuration = ability.PreWindUp;
                    vulnerable.endDuration = ability.RecoveryAfter;
                    vulnerable.destroyTilesWithinRadius = ability.BreaksTerrainWithin;
                    vulnerable.pushBackNearbyEntitiesForce = ability.ShovesNearbyWithForce;
                    vulnerable.pushBackNearbyEntitiesForceRadius = ability.ShoveReaches;
                    vulnerable.maxHealthRatioLostToLeaveState =
                        ability.LeavesAfterLosingThisMuchHealth;
                    break;
                }
            }
        }

        /// <summary>
        /// The run's binder: the game's own numbers baked, this mod's own names left for the game.
        /// </summary>
        private static DimensionObjectBinder binder = new DimensionObjectBinder(default);

        /// <summary>
        /// Resolves an object name at generation time to one of the GAME's own numbers.
        /// </summary>
        /// <remarks>
        /// This was a fifth hand-rolled copy of the resolver, and the half of it that asked
        /// <c>API.Authoring.GetObjectID</c> could never answer: that lookup is a runtime dictionary
        /// and is empty at generation time. So every one of the mod's own names came back None here
        /// and every caller read that as a mistake. Ownership is asked separately, of the binder.
        /// </remarks>
        private static ObjectID ResolveObject(string itemId)
        {
            return DimensionObjectBinder.Vanilla(itemId);
        }

        /// <summary>True when the name is one of this mod's own and the runtime will fill it in.</summary>
        private static bool IsDeferred(string itemId)
        {
            return binder.IsDeferred(itemId);
        }

        private static void ApplyDeath(GameObject root, Request request)
        {
            DimensionCreatureStatsTemplate stats = request.Stats;
            if (!stats.OverrideDeathTiming && !stats.SkipDeathAnimation)
            {
                return;
            }

            DeathStateAuthoring death = EnsureComponent<DeathStateAuthoring>(root);
            death.overrideTimeBeforeDestroy = stats.OverrideDeathTiming;
            if (stats.OverrideDeathTiming)
            {
                death.timeBeforeDestroy = stats.TimeBeforeDestroy;
                death.timeBeforeLootDrop = stats.TimeBeforeLootDrop;
            }

            death.skipDeathAnimation = stats.SkipDeathAnimation;
        }

        /// <summary>
        /// Points the creature at the Core Keeper behaviour it borrows.
        /// </summary>
        /// <remarks>
        /// An unrecognised name is reported rather than guessed at. Falling back to some default AI
        /// would produce a creature that spawns, moves and fights — just not remotely as intended,
        /// which is far harder to notice than one that never spawns.
        /// </remarks>
        private static void ApplyBehaviour(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            if (string.IsNullOrEmpty(request.BehaviourName))
            {
                RemoveComponentIfPresent<BehaviourAuthoring>(root);
                return;
            }

            BehaviourObjectID behaviourId;
            if (!Enum.TryParse(request.BehaviourName, false, out behaviourId))
            {
                report.Warnings.Add(
                    request.CreatureId + " asks for behaviour '" + request.BehaviourName +
                    "', which this version of the game does not have. It will spawn with no behaviour " +
                    "and stand still.");
                RemoveComponentIfPresent<BehaviourAuthoring>(root);
                return;
            }

            BehaviourAuthoring behaviour = EnsureComponent<BehaviourAuthoring>(root);
            behaviour.objectID = behaviourId;

            // Only the robot patroller behaviour reads these, and it reads them off the component
            // rather than off its own mortar states — so a creature borrowing that behaviour had no
            // way to change what its mortars did.
            DimensionCreatureCombatTemplate combat = request.Combat;
            if (combat != null)
            {
                behaviour.robotPatrollerBehaviourSettings =
                    new BehaviourAuthoring.RobotPatrollerBehaviourSettings
                    {
                        oilMortarDamage = combat.OilMortarFlatDamage,
                        oilMortarDamageMultiplier = combat.OilMortarMultiplier,
                        oilMortarTileDamage = combat.OilMortarFlatTerrainDamage,
                        oilMortarTileDamageMultiplier = combat.OilMortarTerrainMultiplier,
                        fireMortarDamage = combat.FireMortarFlatDamage,
                        fireMortarDamageMultiplier = combat.FireMortarMultiplier,
                        fireMortarTileDamage = combat.FireMortarFlatTerrainDamage,
                        fireMortarTileDamageMultiplier = combat.FireMortarTerrainMultiplier
                    };
            }
        }

        /// <summary>
        /// What this creature drops: its own loot table, plus anything that named it as a source.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Two independent things, and a creature may have either or both. The loot table is the
        /// creature's own; the custom loot is the other half of the drop-location inversion — every
        /// item that said "I drop off this" collected into one place by
        /// <c>DimensionDropCollector</c>.
        /// </para>
        /// <para>
        /// Drops the emitter cannot carry — an amount range or a biome restriction, neither of which
        /// per-object custom loot supports — are handed back and put on the report, so the bootstrap
        /// step can register them against the creature's loot table at load instead. They must not be
        /// dropped here: silently losing a range is exactly the failure the routing exists to prevent.
        /// </para>
        /// </remarks>
        private static void ApplyLoot(
            GameObject root,
            Request request,
            DimensionCreatureGenerationReport report)
        {
            bool hasTable = request.LootTable != null;
            bool hasCollectedDrops = request.DropsFromItems != null &&
                request.DropsFromItems.Drops.Count > 0;

            // TAKEN BACK ABOVE THE EARLY RETURN. It used to sit below it, so a creature that had
            // its loot table and its drops cleared but still shed ore kept last generate's table
            // stamped on it — and the "you are using the game's own table" warning then named a
            // table the author had already deleted.
            DimensionDropEmitter.ClearAnyLootTheLastGenerateWrote(root);

            // Pet and extra loot run BEFORE the no-loot early return. A pet mob without a
            // death loot table is the normal shape of a pet mob, and both used to sit below
            // the return — so a creature with no loot silently never became a pet and never
            // carried its extra loot, with no warning anywhere.
            DimensionObjectSpine.ApplyPet(
                root,
                request.Pet,
                delegate(string message)
                {
                    report.Warnings.Add("'" + request.DisplayName + "' " + message);
                });

            if (!hasTable && !hasCollectedDrops)
            {
                DimensionObjectSpine.ApplyExtraLoot(
                    root,
                    request.ExtraLoot,
                    delegate(string objectId) { return ResolveObject(objectId); },
                    delegate(string message)
                    {
                        report.Warnings.Add("'" + request.DisplayName + "' " + message);
                    },
                    IsDeferred);

                // The removal is CONDITIONAL now. Extra loot may have just added the component —
                // shedding ore as you mine it is the normal shape of a creature with no death loot
                // table — and taking it straight back off threw that away a line after it was
                // written, with nothing said.
                if (request.ExtraLoot == null || !request.ExtraLoot.WantsAnExtraChannel)
                {
                    RemoveComponentIfPresent<DropLootAuthoring>(root);
                }
                return;
            }

            DropLootAuthoring loot = EnsureComponent<DropLootAuthoring>(root);

            DimensionObjectSpine.ApplyExtraLoot(
                root,
                request.ExtraLoot,
                delegate(string objectId) { return ResolveObject(objectId); },
                delegate(string message)
                {
                    report.Warnings.Add("'" + request.DisplayName + "' " + message);
                },
                IsDeferred);

            if (hasTable)
            {
                // The mod's own table's id is minted from the name by pure arithmetic, so the
                // prefab can carry it now and the runtime registry builds the table under the same
                // id at load — the two never need to meet. LootTableIdFor is the shared answer the
                // bootstrap emitter also ships, so a drop row and this prefab cannot disagree.
                string tableName = DimensionDropEmitter.AuthoredLootTableNameOf(request.LootTable);
                if (!string.IsNullOrEmpty(tableName))
                {
                    loot.hasLootTable = true;
                    loot.lootTableID = DimensionDropEmitter.LootTableIdFor(tableName);
                }
                else if (!string.IsNullOrEmpty(request.LootTable.LootTableId))
                {
                    report.Warnings.Add(
                        "'" + request.DisplayName + "' points at loot table '" +
                        request.LootTable.LootTableId + "', which is neither one of the game's nor " +
                        "one of yours with anything in it, so it drops nothing from a table. Put " +
                        "rows in that table, or point at one of the game's.");
                }
            }

            if (!hasCollectedDrops)
            {
                return;
            }

            // A creature that points at a loot table shares it: the drops go into a table this mod
            // does not get to reshape, so a chance under 1 cannot be made true there and the report
            // has to say so rather than promise the number works.
            List<DimensionResolvedDrop> needsATable = DimensionDropEmitter.ApplyCustomLoot(
                root,
                request.DropsFromItems,
                delegate(string itemId) { return ResolveObject(itemId); },
                delegate(string message) { report.Warnings.Add(message); },
                IsDeferred,
                !loot.hasLootTable);

            for (int i = 0; i < needsATable.Count; i++)
            {
                report.DropsNeedingALootTable.Add(needsATable[i]);
            }

            // A creature with drops that only a loot table can carry NEEDS a loot table, and most
            // creatures are authored without one. Given none it used to carry no
            // DropsLootFromLootTableCD at all, so the drop had nowhere to be registered at load and
            // simply never happened.
            if (needsATable.Count > 0)
            {
                DimensionDropEmitter.EnsureALootTableToHangDropsOn(
                    root,
                    binder.Naming.QualifyGenerated(request.CreatureId),
                    delegate(string message) { report.Warnings.Add(message); });
            }
        }


        private static T EnsureComponent<T>(GameObject root)
            where T : Component
        {
            T component = root.GetComponent<T>();
            return component != null ? component : root.AddComponent<T>();
        }

        private static void RemoveComponentIfPresent<T>(GameObject root)
            where T : Component
        {
            // Routed through the one dependency-aware removal, so a RequireComponent cannot
            // silently defeat authoritative generation. See DimensionObjectSpine.TryRemoveComponent.
            DimensionObjectSpine.TryRemoveComponent<T>(root);
        }

    }
}
