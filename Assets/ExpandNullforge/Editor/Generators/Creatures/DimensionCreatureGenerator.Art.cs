using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// The picture side of a creature: its view, its name plate, and the prefabs beside it.
    /// </summary>
    internal static partial class DimensionCreatureGenerator
    {
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
    }
}
