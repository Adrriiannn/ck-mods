using UnityEditor;
using ExpandNullforge.Authoring;
using Interaction;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Where an object may be put, and what the world does with it once it is there.
    /// </summary>
    internal static partial class DimensionObjectSpine
    {
        /// <summary>
        /// What a placed, player-facing object gets: paint, facing, and a description.
        /// </summary>
        /// <remarks>
        /// Vanilla chests carry all three. Paint is the visible one — a custom chest that cannot take
        /// a paint bucket sits oddly beside vanilla ones in the same base.
        /// </remarks>
        public static void ApplyPlacedObject(GameObject root, bool paintable, bool rotatable)
        {
            EnsureComponent<DescriptionAuthoring>(root);

            if (paintable)
            {
                EnsureComponent<PaintableObjectAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<PaintableObjectAuthoring>(root);
            }

            if (rotatable)
            {
                EnsureComponent<RotationAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<RotationAuthoring>(root);
            }
        }

        /// <summary>
        /// The placed-object kinds that are a marker or one reference.
        /// </summary>
        /// <remarks>
        /// A fence gate is a bare marker. A flower names the plant it belongs to. A spawner platform
        /// names the enemy it produces — which makes it the simplest piece of dungeon furniture
        /// there is, and one a modder is very likely to want.
        /// </remarks>
        public static void ApplyPlacedKinds(
            GameObject root,
            bool isAFenceGate,
            string flowerOfPlantId,
            int flowerVariation,
            string spawnsEnemyId,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null)
        {
            Toggle<FenceGateAuthoring>(root, isAFenceGate);

            if (string.IsNullOrEmpty(flowerOfPlantId))
            {
                RemoveComponentIfPresent<FlowerAuthoring>(root);
            }
            else
            {
                ObjectID plant = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(flowerOfPlantId);
                bool plantIsOneOfOurs =
                    plant == ObjectID.None &&
                    isDeferred != null &&
                    isDeferred(flowerOfPlantId);
                if (plant == ObjectID.None && !plantIsOneOfOurs)
                {
                    if (report != null)
                    {
                        report(
                            "is the flower of '" + flowerOfPlantId + "', which is neither one of " +
                            "this mod's objects nor one the game has, so it belongs to nothing.");
                    }

                    RemoveComponentIfPresent<FlowerAuthoring>(root);
                }
                else
                {
                    // The component STAYS for one of the mod's own plants. FlowerConverter writes
                    // FlowerCD whatever the id is, and that component is what the link hydration
                    // fills in at load; stripping it would leave nothing to write into.
                    FlowerAuthoring flower = EnsureComponent<FlowerAuthoring>(root);
                    flower.plantID = plant;
                    flower.plantVariation = flowerVariation < 0 ? 0 : flowerVariation;
                }
            }

            if (string.IsNullOrEmpty(spawnsEnemyId))
            {
                RemoveComponentIfPresent<EnemySpawnerPlatformAuthoring>(root);
                return;
            }

            ObjectID enemy = resolveObject == null
                ? ObjectID.None
                : resolveObject(spawnsEnemyId);
            if (enemy == ObjectID.None && !(isDeferred != null && isDeferred(spawnsEnemyId)))
            {
                if (report != null)
                {
                    report(
                        "spawns '" + spawnsEnemyId + "', which is neither one of this mod's " +
                        "creatures nor one the game has, so it will spawn nothing.");
                }

                RemoveComponentIfPresent<EnemySpawnerPlatformAuthoring>(root);
                return;
            }

            EnsureComponent<EnemySpawnerPlatformAuthoring>(root).enemyToSpawn = enemy;
        }

        /// <summary>
        /// A waypoint players can travel to.
        /// </summary>
        /// <remarks>
        /// The core flag is worth guarding rather than trusting: it marks the one waypoint a player
        /// returns to, and a mod that sets it on several is a mod where "go home" is ambiguous.
        /// </remarks>
        public static void ApplyWaypoint(
            GameObject root,
            bool isAWaypoint,
            float activateWithin,
            bool isTheCoreWaypoint)
        {
            if (!isAWaypoint)
            {
                RemoveComponentIfPresent<WayPointAuthoring>(root);
                return;
            }

            WayPointAuthoring waypoint = EnsureComponent<WayPointAuthoring>(root);
            waypoint.distanceToActivate = activateWithin;
            waypoint.isCoreWaypoint = isTheCoreWaypoint;
        }

        /// <summary>
        /// <summary>
        /// The small rules the world applies to a placed object.
        /// </summary>
        /// <remarks>
        /// Seven components, most of them bare markers. Every one is added or removed rather than
        /// only added, so an author who unticks something gets the object they see in the inspector.
        /// </remarks>
        /// <summary>
        /// The last of the placed-object components: paint, scanning, map pins, lifetimes,
        /// variation-driven facing, and the table condition.
        /// </summary>
        /// <remarks>
        /// These are one and two field components rather than systems of their own, so they are
        /// written together here instead of each getting a template. Every one of them was attached
        /// somewhere and left blank, which is the quiet half of the coverage problem: the object
        /// carries the component, the game reads it, and it says nothing.
        /// </remarks>
        private static void ApplyTheRestOfTheObjectRules(
            GameObject root,
            DimensionObjectRulesTemplate rules,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report)
        {
            PaintableObjectAuthoring paintable = root.GetComponent<PaintableObjectAuthoring>();
            if (paintable != null)
            {
                PaintableColor colour;
                if (System.Enum.TryParse(rules.StartingPaintColour, false, out colour))
                {
                    paintable.color = colour;
                }
                else if (report != null)
                {
                    report(
                        "starts painted '" + rules.StartingPaintColour + "', which is not a colour " +
                        "the game paints things, so it starts unpainted.");
                }
            }

            CanBeScannedAuthoring scanned = root.GetComponent<CanBeScannedAuthoring>();
            if (scanned != null && !string.IsNullOrEmpty(rules.ScannerReportsObjectId))
            {
                ObjectID reported = resolveObject == null
                    ? ObjectID.None
                    : resolveObject(rules.ScannerReportsObjectId);
                if (reported != ObjectID.None)
                {
                    scanned.objectData = new ObjectData
                    {
                        objectID = reported,
                        variation = rules.ScannerReportsVariation
                    };
                }
                else if (report != null)
                {
                    report(
                        "reports as '" + rules.ScannerReportsObjectId + "' to a scanner, which the " +
                        "game does not have, so it reports as itself.");
                }
            }

            // THE COMPONENT IS ADDED HERE, NOT LOOKED UP. Nothing else in this framework ADDS
            // DontDestroyOnZeroHealthAuthoring — the only other mention of it is a
            // RemoveComponentIfPresent on the framework's own portal — so a plain GetComponent
            // reads the tickbox, finds no component, and does nothing at all, for every object
            // ever generated. `DontDestroyOnZeroHealthConverter` adds `AnimateDontDestroyOnZeroHealthCD`
            // when animate is on, and `AnimateDontDestroyOnZeroHealthSystem` needs only that, the
            // health, the animation buffer and its pointer — which anything with health already
            // carries — so putting the component on is the whole of the fix, and the two clips it
            // fires ("feignDeath" at `:93` and "revive" at `:97`) become drawable.
            //
            // ONLY ON THE WAY ON. Ticking it changes what the object IS — with
            // `DontDestroyOnZeroHealthCD` present and not disabled, `SetEntitiesDestroyedSystem`
            // returns before it ever marks the object destroyed (`:163`), so the object survives
            // zero health and drops nothing. Unticking it therefore only clears the animate flag
            // and leaves a component a borrowed kit may have put there alone, rather than quietly
            // making something mortal that was authored not to be.
            DontDestroyOnZeroHealthAuthoring survives = rules.AnimatesWhenItWouldHaveDied
                ? EnsureComponent<DontDestroyOnZeroHealthAuthoring>(root)
                : root.GetComponent<DontDestroyOnZeroHealthAuthoring>();
            if (survives != null)
            {
                survives.animate = rules.AnimatesWhenItWouldHaveDied;
            }

            SayWhenTicked(
                rules.AnimatesWhenItWouldHaveDied,
                report,
                "plays dead instead of dying. At zero health the game stops short of destroying " +
                "it: it drops, and it stands back up if anything ever heals it. It will not drop " +
                "loot and it will not disappear, because it never actually dies, so something " +
                "else has to take it away. Draw 'Playing dead' and 'Getting back up' for it.");

            ImmuneToSkipLootDropAuthoring lootImmune =
                root.GetComponent<ImmuneToSkipLootDropAuthoring>();
            if (lootImmune != null)
            {
                lootImmune.ignoredInCreativeMode = rules.CreativeIgnoresItsLootImmunity;
            }

            InteractWithEnvironmentAuthoring environment =
                root.GetComponent<InteractWithEnvironmentAuthoring>();
            if (environment != null)
            {
                environment.disableComponent =
                    new Pug.UnityExtensions.PlatformDependentValue<bool>(
                        rules.EnvironmentInteractionIsOff);
            }

            AnimationAuthoring animation = root.GetComponent<AnimationAuthoring>();
            if (animation != null)
            {
                animation.largeAnimationHistorySupport = rules.SupportsLongAnimationHistory;
            }

            AreaLevelAuthoring level = root.GetComponent<AreaLevelAuthoring>();
            if (level != null)
            {
                level.forceGenerateLevelEntities = rules.ForcesLevelEntities;
            }

            ImmunityZoneAuthoring zone = root.GetComponent<ImmunityZoneAuthoring>();
            if (zone != null)
            {
                zone.tileOffset = new Unity.Mathematics.int2(
                    rules.ImmunityZoneTileOffset.x,
                    rules.ImmunityZoneTileOffset.y);
            }

            MapMarkerAuthoring marker = root.GetComponent<MapMarkerAuthoring>();
            if (marker != null)
            {
                // uniqueMarkerId is an ObjectID, not a name: it is the object whose single map pin
                // this one shares, which is how a multi-part structure shows up once.
                marker.uniqueMarkerId = string.IsNullOrEmpty(rules.SharedMapMarkerId)
                    ? ObjectID.None
                    : (resolveObject == null ? ObjectID.None : resolveObject(rules.SharedMapMarkerId));

                if (!string.IsNullOrEmpty(rules.SharedMapMarkerId) &&
                    marker.uniqueMarkerId == ObjectID.None &&
                    report != null)
                {
                    report(
                        "shares its map pin with '" + rules.SharedMapMarkerId + "', which the " +
                        "game does not have, so it gets its own pin.");
                }
                UserMapMarkerType markerKind;
                if (System.Enum.TryParse(rules.PlayerMarkerKind, false, out markerKind))
                {
                    marker.userMapMarkerType = markerKind;
                }
                else if (report != null)
                {
                    report(
                        "shows as map marker kind '" + rules.PlayerMarkerKind + "', which the game " +
                        "does not have, so it shows as an ordinary pin.");
                }
            }

            DirectionBasedOnVariationAuthoring facing =
                root.GetComponent<DirectionBasedOnVariationAuthoring>();
            if (facing != null)
            {
                facing.direction = new Unity.Mathematics.int2(
                    rules.FacingFromVariation.x,
                    rules.FacingFromVariation.y);
                // ALWAYS FALSE, and the author is told why. It is not a crash: every
                // generated object says its look is not part of its identity
                // (DimensionQueryCompanions.ItsLookIsNotPartOfItsIdentity), so
                // ColliderBasedOnDirectionVariationSystem's lookup returns this object's own
                // prefab instead of Entity.Null. What it would then copy is the collider the
                // object already has, because this framework writes one prefab per object and that
                // prefab has one shape — so the switch could only ever pretend to do something.
                //
                // THE AUTHOR'S ANSWER IS NOT LOST. "Its hitbox turns with it" is carried by
                // RotationAuthoring.rotatePhysics instead, which RotationPostConverter turns into
                // four rotated shapes and RotateColliderSystem picks between by direction. That is
                // a real turned hitbox rather than a second look that does not exist.
                facing.alsoUpdateCollider = false;
                facing.alignWithNearbyAffectorsWhenPlaced = rules.LinesUpWithNeighboursWhenPlaced;

                SayWhenTicked(
                    rules.ColliderTurnsWithIt,
                    report,
                    "is set to turn its hitbox with it. Its hitbox is turned by rotating the one " +
                    "shape it has, which is what the game does for anything that faces a " +
                    "direction. The other road — looking the object up under a second look and " +
                    "taking that one's hitbox — is switched off, because this framework builds " +
                    "one look per object and there is no second hitbox to find.");
            }

            DestroyTimerAuthoring timer = root.GetComponent<DestroyTimerAuthoring>();
            if (timer != null)
            {
                timer.disablePhysicsAfterDuration = rules.PhysicsStopAfterSeconds;
                timer.dontDropLootAfterTimerRunsOut = rules.DropsNothingWhenItsTimeIsUp;
                timer.startTimerWhenVariation = rules.LifetimeStartsAtVariation;
            }

            TableItemLightSourceAuthoring table =
                root.GetComponent<TableItemLightSourceAuthoring>();

            // A LAMP WITH NO GLOW LIGHTS NOTHING, and that was the whole of it. Core Keeper decides
            // whether an object standing on a table gives off light by looking at the glow written
            // here: one of the five glow colours, with its strength used as the RANGE. Left blank,
            // the check falls through every branch, the light is switched off and the object sits
            // there dark. The game's own torch ships an orange glow at a range of two.
            if (table != null && string.IsNullOrEmpty(rules.TableConditionId) && report != null)
            {
                report(
                    "lights the room where it is placed but was not given a glow, and the glow is " +
                    "what the light is made of: without one the game leaves the light switched " +
                    "off. Set the table glow to one of the game's glow colours and give it a " +
                    "strength, which is how far the light reaches. The game's own torch uses an " +
                    "orange glow at two.");
            }

            if (table != null && !string.IsNullOrEmpty(rules.TableConditionId))
            {
                ConditionID tableCondition;
                if (System.Enum.TryParse(rules.TableConditionId, false, out tableCondition))
                {
                    table.Condition = new SimpleConditionData
                    {
                        conditionID = tableCondition,
                        value = rules.TableConditionStrength
                    };
                }
                else if (report != null)
                {
                    report(
                        "gives condition '" + rules.TableConditionId + "' from a table, which the " +
                        "game does not have, so it gives nothing.");
                }
            }
        }

        public static void ApplyObjectRules(
            GameObject root,
            DimensionObjectRulesTemplate rules,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred = null,
            System.Func<string, string> qualify = null)
        {
            if (rules == null)
            {
                rules = new DimensionObjectRulesTemplate();
            }

            Toggle<AlwaysDropVariationZeroAuthoring>(root, rules.AlwaysDropsItsFirstVariation);
            Toggle<GroundDecorationAuthoring>(root, rules.IsGroundCover);

            ApplyTheRestOfTheObjectRules(root, rules, resolveObject, report);

            if (rules.AScannerFindsIt)
            {
                EnsureComponent<CanBeScannedAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<CanBeScannedAuthoring>(root);
            }

            // alwaysEnabled defaults to true on the component, so the tick is only worth writing
            // when it is being turned OFF - otherwise every object carries a component saying what
            // would have happened anyway.
            if (!rules.AlwaysStaysEnabled)
            {
                EnsureComponent<CustomDisableAuthoring>(root).alwaysEnabled = false;
            }
            else
            {
                RemoveComponentIfPresent<CustomDisableAuthoring>(root);
            }

            if (rules.OverridesNetworkRange)
            {
                EnsureComponent<OverrideNetworkSyncDistanceAuthoring>(root).distance =
                    rules.NetworkRange;
            }
            else
            {
                RemoveComponentIfPresent<OverrideNetworkSyncDistanceAuthoring>(root);
            }

            ApplyPlacementRule(root, rules, resolveObject, report, isDeferred, qualify);
            ApplyMapMarker(root, rules, report);
        }

        private static void ApplyPlacementRule(
            GameObject root,
            DimensionObjectRulesTemplate rules,
            System.Func<string, ObjectID> resolveObject,
            System.Action<string> report,
            System.Func<string, bool> isDeferred,
            System.Func<string, string> qualify)
        {
            if (!rules.HasAPlacementRule)
            {
                RemoveComponentIfPresent<DestroyIfNotOnTileAuthoring>(root);
                ClearPlacedOnTileNames(root);
                return;
            }

            if (rules.MayStandNowhere && report != null)
            {
                report(
                    "has a rule about where it may stand that matches no tile at all, so it will " +
                    "destroy itself the moment it is placed.");
            }

            DestroyIfNotOnTileAuthoring rule = EnsureComponent<DestroyIfNotOnTileAuthoring>(root);
            ObjectID stands = resolveObject == null
                ? ObjectID.None
                : resolveObject(rules.MustStandOnObjectId);

            // ONE OF THE MOD'S OWN TILE OBJECTS RIDES AS A NAME. validTileObject ends up in the
            // sealed property blob (CanBePlaced/allowedObjects), which no system can reach on any
            // tick — so unlike every other reference in the framework this one cannot be filled in
            // at load. DimensionPlacedOnNamesAuthoring carries the name instead and the placement
            // post-converter writes the list once every object has an id. The component existed and
            // its three tile fields were tooltipped, public, and written by nothing.
            bool standsOnOneOfOurs =
                stands == ObjectID.None &&
                isDeferred != null &&
                isDeferred(rules.MustStandOnObjectId);

            if (standsOnOneOfOurs)
            {
                ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring names =
                    EnsureComponent<ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring>(root);
                names.allowedTileObjectNames = new string[]
                {
                    qualify == null
                        ? rules.MustStandOnObjectId
                        : qualify(rules.MustStandOnObjectId)
                };
            }
            else
            {
                ClearPlacedOnTileNames(root);
            }

            if (stands == ObjectID.None && !standsOnOneOfOurs &&
                !string.IsNullOrEmpty(rules.MustStandOnObjectId) &&
                report != null)
            {
                report(
                    "must stand on '" + rules.MustStandOnObjectId + "', which is neither one of " +
                    "this mod's objects nor one the game has, so it will destroy itself wherever " +
                    "it is placed.");
            }

            rule.validTileObject = stands;
            rule.canBePlacedOnAnyWalkableTile = rules.AnyWalkableTileWillDo;
            rule.canBePlacedOnWater = rules.MayStandOnWater;
            rule.canBePlacedOnLava = rules.MayStandOnLava;
            rule.canBePlacedOnPit = rules.MayStandOverAPit;
        }

        /// <summary>
        /// Takes back the tile names this pass wrote, without touching the other two lists.
        /// </summary>
        /// <remarks>
        /// The component is shared with the vehicle generator's "also goes on" list, so removing it
        /// outright would delete somebody else's answer. Only the fields this pass owns are cleared,
        /// and the component goes only when nothing is left on it.
        /// </remarks>
        private static void ClearPlacedOnTileNames(GameObject root)
        {
            ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring names =
                root.GetComponent<ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring>();
            if (names == null)
            {
                return;
            }

            names.allowedTileObjectNames = new string[0];
            if (names.canBePlacedOnNames == null || names.canBePlacedOnNames.Length == 0)
            {
                RemoveComponentIfPresent<
                    ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring>(root);
            }
        }

        private static void ApplyMapMarker(
            GameObject root,
            DimensionObjectRulesTemplate rules,
            System.Action<string> report)
        {
            if (!rules.ShowsOnTheMap)
            {
                RemoveComponentIfPresent<MapMarkerAuthoring>(root);
                return;
            }

            MapMarkerType marker;
            if (!System.Enum.TryParse(rules.MapMarker, false, out marker))
            {
                if (report != null)
                {
                    report(
                        "shows on the map as '" + rules.MapMarker + "', which is not a marker the " +
                        "game has, so it will not show at all.");
                }

                RemoveComponentIfPresent<MapMarkerAuthoring>(root);
                return;
            }

            MapMarkerAuthoring authored = EnsureComponent<MapMarkerAuthoring>(root);
            authored.mapMarkerType = marker;
            authored.hideWhenDiscovered = rules.MarkerGoesOnceFound;
        }

        /// <summary>
        /// Writes a mortar barrage onto an existing <c>ShootMortarProjectileStateAuthoring</c>.
        /// </summary>
        /// <summary>
        /// Writes where a placed object may go onto an existing <c>PlaceableObjectAuthoring</c>.
        /// </summary>
        /// <remarks>
        /// Never adds the component. Whether a thing is placeable at all is decided by what kind of
        /// thing it is, upstream of this — a creature is not made placeable by filling in a
        /// placement panel.
        /// </remarks>
        /// <summary>
        /// Writes the three non-death loot channels onto an existing <c>DropLootAuthoring</c>.
        /// </summary>
        /// <remarks>
        /// The "has" flags are derived from whether the author filled each section in, never asked
        /// about separately. The game reads the flag rather than the data, so a section filled in
        /// with the flag off drops nothing and says nothing — a failure worth making unauthorable.
        /// </remarks>
        /// <summary>
        /// Makes an object react to a melody played near it — Core Keeper's ocarina system.
        /// </summary>
        /// <remarks>
        /// Adds the component only when a melody was actually named. An object listening for
        /// nothing is a component the game polls forever to no effect.
        /// </remarks>
        /// <summary>
        /// Makes an object something a machine can pull resources out of indefinitely.
        /// </summary>
        /// <summary>
        /// Makes an object react to what comes near it — the game's whole lock-and-key vocabulary.
        /// </summary>
        /// <summary>
        /// The small roles an object plays in a base — sitting, naming, painting, resizing,
        /// being discovered, and being un-hittable.
        /// </summary>
        /// <remarks>
        /// Several of these components hold no settings at all: their presence IS the setting, so
        /// they are toggled rather than filled in.
        /// </remarks>
        /// <summary>
        /// The habits that make a creature an inhabitant rather than a monster — loitering,
        /// taunting, guarding a nest, being kept, getting full.
        /// </summary>
        /// <summary>
        /// Makes something a trader, money, a soul, or a seasonal object.
        /// </summary>
        /// <summary>
        /// Makes something a nest that keeps producing creatures or objects around itself.
        /// </summary>
        /// <summary>
        /// Makes an object pick its look from the ground around it — bridges meeting shores, rails
        /// knowing they are a corner.
        /// </summary>
        /// <summary>
        /// Machines that work on their own — drills, automated miners, belt filters — and the
        /// workshop roles that go with them.
        /// </summary>
        /// <summary>
        /// Makes a creature a body that follows its head — a worm, a serpent, a caterpillar.
        /// </summary>
        /// <summary>
        /// The remaining world roles — shrines, summoning items, fireflies, graves, spreading fire,
        /// barriers, minions and free-moving weapons.
        /// </summary>
        /// <remarks>
        /// Nine of the components behind this hold no settings at all, so they are toggled. The rest
        /// carry one or two values each.
        /// </remarks>
        /// <summary>
        /// Makes a weapon fire a held beam rather than a projectile.
        /// </summary>
        /// <summary>
        /// Makes a creature attack the world in its way — walls, buildings, scenery.
        /// </summary>
        /// <remarks>
        /// Separate from the melee swing on purpose: hitting a player and hitting a wall are two
        /// states with two damage numbers, which is how a thing that chews through a base to reach
        /// you is built.
        /// </remarks>
        /// <summary>
        /// Gives a custom boss a kit borrowed from one of the game's own.
        /// </summary>
        /// <remarks>
        /// The borrow door. A boss given the Hydra's kit burrows, surfaces and fires the same six
        /// attacks; leave every number alone and it fights exactly like a Hydra with a different
        /// sprite, or change one attack and only that attack changes.
        /// </remarks>
        /// <summary>
        /// Gives a custom boss the Core's, the Wall's or the Scarab's fight.
        /// </summary>
        /// <summary>
        /// Gives a custom boss the Bird's, Robot's, Octopus's, Larva's, Shaman's or Snake's fight.
        /// </summary>
        /// <summary>
        /// More ways a creature fights: a sweeping ray, contact damage, a shield, placing objects,
        /// and orbiting its owner.
        /// </summary>
        /// <summary>
        /// A chain of explosions, a trail, timed look changes, and the odds and ends.
        /// </summary>
        /// <summary>
        /// Plain spawners, drifting orbs, followers, statues, terrain-chewing roamers and gravity
        /// wells.
        /// </summary>
        /// <summary>
        /// Mana pools and siphons, healing auras, ancient wiring, ownership, boss hooks, and blast
        /// ground.
        /// </summary>
        /// <summary>
        /// Bush-hiding, eggs, caveling territories, delayed shots, proximity triggers, animation
        /// speed and the extra inventory slots.
        /// </summary>
        /// <summary>
        /// A creature's own beam attack, alerts, dripping items, catching fire, ambient swimming
        /// and scuttling, note mimicry, and corner smoothing.
        /// </summary>
        /// <summary>
        /// The last small behaviours — seats, reacting to wounds, pheromones, boss beams and spawn
        /// points, hive eggs.
        /// </summary>
        /// <summary>
        /// The game's own way of putting a one-off somewhere when a world is made.
        /// </summary>
        /// <remarks>
        /// Two of the answers are asked twice because a world is either classic or full release and
        /// the two generate differently. Creative worlds read the classic answer.
        /// </remarks>
        public static void ApplyNativeWorldPlacement(
            GameObject root,
            DimensionNativeWorldPlacementTemplate placement,
            System.Action<string> report)
        {
            if (root == null || placement == null)
            {
                return;
            }

            if (!placement.PlacesSomethingWhenAWorldIsMade || placement.NothingToPlace)
            {
                RemoveComponentIfPresent<PugWorldGen.PugWorldGenAuthoring>(root);

                if (report != null && placement.NothingToPlace)
                {
                    report(
                        "is a world-generation marker with nothing named to place, so generation " +
                        "would run it and put nothing in the world.");
                }

                return;
            }

            PugWorldGen.PugWorldGenAuthoring worldGen =
                EnsureComponent<PugWorldGen.PugWorldGenAuthoring>(root);

            worldGen.prefab = placement.TheThingToPlace;
            worldGen.markerPrefab = placement.TheMarkerThatPlacesIt;
            worldGen.spawnImmediatelyOnLoad = placement.AppearsAsSoonAsTheWorldLoads;
            worldGen.destroyMarkerAfterSpawn = placement.ClearsTheMarkerAwayAfterwards;
            worldGen.sortIndex = placement.Order;

            worldGen.placementType = (UniqueScenePlacementType)(int)placement.HowTheSpotIsChosen;
            worldGen.positionSampling =
                (UniqueScenePositionSampling)(int)placement.WhereInsideTheBiome;
            worldGen.allowOverlappingSpawnCellBorders = placement.MayStraddleSpawnCellBorders;

            worldGen.biome = new WorldGenerationTypeDependentValue<Biome>
            {
                classic = ResolveBiomeName(
                    placement.BiomeInAClassicWorld,
                    worldGen.biome.classic,
                    "classic",
                    report),
                fullRelease = ResolveBiomeName(
                    placement.BiomeInAFullReleaseWorld,
                    worldGen.biome.fullRelease,
                    "full-release",
                    report)
            };

            worldGen.targetDistanceFromCore = new WorldGenerationTypeDependentValue<int>
            {
                classic = placement.DistanceFromTheCoreClassic,
                fullRelease = placement.DistanceFromTheCoreFullRelease
            };

            worldGen.spawnPosition = new WorldGenerationTypeDependentValue<Unity.Mathematics.int2>
            {
                classic = new Unity.Mathematics.int2(
                    placement.ExactSpotClassic.x,
                    placement.ExactSpotClassic.y),
                fullRelease = new Unity.Mathematics.int2(
                    placement.ExactSpotFullRelease.x,
                    placement.ExactSpotFullRelease.y)
            };

            worldGen.contentBundle = placement.PartOfContentBundle != null
                ? placement.PartOfContentBundle
                : default(DataBlockRef<ContentBundleDataBlock>);

            worldGen.replacedByBundle = placement.ReplacedByBundle != null
                ? new Pug.UnityExtensions.OptionalValue<DataBlockRef<ContentBundleDataBlock>>(
                    placement.ReplacedByBundle)
                : default(Pug.UnityExtensions.OptionalValue<DataBlockRef<ContentBundleDataBlock>>);

            if (report == null)
            {
                return;
            }

            if (placement.ExactSpotIsOnTopOfTheCore)
            {
                report(
                    "goes on one exact tile and that tile is the origin, which is where the Core " +
                    "sits, so it would be placed on top of the Core.");
            }

            if (placement.ReplacedByItsOwnBundle)
            {
                report(
                    "is replaced by the same content bundle it belongs to, so it replaces itself " +
                    "and never appears.");
            }
        }

        public static void ApplyPlacementRules(
            GameObject root,
            DimensionPlacementRulesTemplate rules,
            System.Action<string> report)
        {
            if (root == null || rules == null)
            {
                return;
            }

            PlaceableObjectAuthoring placeable = root.GetComponent<PlaceableObjectAuthoring>();
            if (placeable == null)
            {
                return;
            }

            placeable.canBePlacedOnBlockingObjects = rules.CanGoOverBlockingObjects;
            placeable.canBePlacedOnImmuneTiles = rules.CanGoOnProtectedTiles;
            placeable.canBePlacedOnLowColliders = rules.CanGoOnLowObstacles;
            placeable.canBePlacedOnLava = rules.CanGoOnLava;

            placeable.canPlaceOnSideOfWall = rules.CanGoOnTheSideOfAWall;
            placeable.hasVariationsThatCanBePlacedOnWalls = rules.HasAWallFacingLook;
            placeable.wallSideVariationStartsOnIndex1 = rules.WallLookStartsAtVariationOne;
            placeable.blocksHangingWallObjects = rules.ClaimsTheWallItIsOn;

            placeable.alignWithPlayerDirection = rules.FacesThePlayersDirection;
            placeable.variationToPlace = rules.VariationPlaced;
            placeable.objectCanBeToggledToNewNonRotationOption = rules.CanBeCycledThroughOtherLooks;
            placeable.toggledToNewNonRotationOptions = rules.HowManyOtherLooks;
            placeable.dontDestroyObjectIfInvalidPlacement = rules.ABadPlacementDoesNotDestroyIt;

            placeable.dontBlockRoots = rules.RootsStillGrowThrough;
            placeable.displayPlaceableType = (DisplayPlaceableType)(int)rules.PlacementPreview;

            if (report == null)
            {
                return;
            }

            if (rules.GoesOnWallsWithNoWallLook)
            {
                report(
                    "can be put on the side of a wall but has no wall-facing look, so it will snap " +
                    "onto the wall and then draw its floor sprite.");
            }

            if (rules.HasAWallLookItCannotUse)
            {
                report(
                    "has a wall-facing look but is not allowed on the side of a wall, so that look " +
                    "can never appear.");
            }

            if (rules.CycleWillBeIgnored)
            {
                report(
                    "lists other looks to cycle through without letting the player cycle it, so " +
                    "only the first is ever seen.");
            }

            if (rules.CycleHasNothingInIt)
            {
                report(
                    "lets the player cycle its look with nothing to cycle to, so the control does " +
                    "nothing when they use it.");
            }
        }
    }
}
