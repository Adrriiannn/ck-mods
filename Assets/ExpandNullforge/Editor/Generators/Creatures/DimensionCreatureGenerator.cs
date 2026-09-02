using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{

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
    internal static partial class DimensionCreatureGenerator
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
            // SIZED FROM THE CREATURE, not from a constant. A hard 1 at the call sites leaves the
            // parameters dead and every creature in every mod the same 0.75 across, whatever it
            // looks like. The two numbers are separate because 24 of the 63 vanilla creatures
            // that carry both shapes size them differently.
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
