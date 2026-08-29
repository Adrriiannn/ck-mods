using System;
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Turns a vehicle definition into the pair of prefabs Core Keeper needs: the entity the
    /// simulation rides, and the body a player walks up to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// THE WHOLE CHAIN, BECAUSE IT USED TO STOP HALFWAY. A vehicle was written as
    /// <c>ObjectType.NonObtainable</c> with no placement, so nothing could ever hold one, put one
    /// down or break one; it existed only where a scene happened to spawn it, and its name reached
    /// nobody. Everything below exists to close one link of that chain, and each link was checked
    /// against the game's own seven vehicles rather than guessed at.
    /// </para>
    /// <para>
    /// HOW A PLAYER ACTUALLY GETS ON, which is the link everything else hangs off. It is not a
    /// UnityEvent on the body the way a chest or an NPC is — it is pure ECS.
    /// <c>UpdateSelectedInteractableSystem</c> picks the nearest entity with an <c>InteractableCD</c>;
    /// pressing use makes <c>TriggerUseInteractionSystem</c> append to that entity's
    /// <c>TriggerUseInteractionBuffer</c>; and <c>TriggerUseControllableSystem</c> reads that buffer,
    /// sees <c>BoatCD</c>/<c>MinecartCD</c>/<c>VehicleCD</c> beside a <c>ControlledByOtherEntityCD</c>,
    /// and puts the player into the matching riding state. Not one step of that consults an ObjectID,
    /// so a vehicle this framework invents is ridden by exactly the same code as the game's own. What
    /// the generator therefore has to guarantee is only this: the three components that make the
    /// buffer exist, the marker that makes the claim possible, and a body carrying an
    /// <c>InteractableObject</c> so <c>InteractablePostConverter</c> has something to build
    /// <c>InteractableCD</c> from.
    /// </para>
    /// <para>
    /// WHERE EACH KIND MAY BE PUT DOWN IS NOT A PREFERENCE, so it is not asked as one. Reading the
    /// game's prefabs: both boats carry <c>canBePlacedOnObjects: [Water]</c> with
    /// <c>canBePlacedOnAnyWalkableTile</c> off; both minecarts carry
    /// <c>canBePlacedOnObjects: [Rail]</c>, also off; all three go-karts have
    /// <c>canBePlacedOnAnyWalkableTile</c> on. Those rules ARE the kind — a boat you could set down in
    /// a corridor, or a minecart off its track, is a vehicle that cannot move — so the generator
    /// writes them and an author adds to them rather than replacing them.
    /// </para>
    /// </remarks>
    internal static class DimensionVehicleGenerator
    {
        /// <summary>The folder generated vehicles are written into, under the items folder.</summary>
        public const string FolderName = "Vehicles";

        /// <summary>
        /// The most damage one hit may do to a vehicle.
        /// </summary>
        /// <remarks>
        /// All seven vanilla vehicles cap at one, which is what turns "how many hits to break" into a
        /// number an author can reason about instead of a race between health and weapon damage.
        /// </remarks>
        private const int DamagePerHit = 1;

        /// <summary>
        /// Whichever object the game itself demands under each kind of vehicle.
        /// </summary>
        /// <remarks>
        /// <c>ObjectID.Water</c> for a boat, <c>ObjectID.Rail</c> for a minecart, and nothing extra for
        /// a go-kart, which instead gets any walkable tile. Transcribed from
        /// <c>BoatEntity.prefab</c>, <c>MinecartEntity.prefab</c> and <c>SpeederGoKartEntity.prefab</c>.
        /// </remarks>
        private static ObjectID GroundItNeeds(DimensionVehicleKind kind)
        {
            switch (kind)
            {
                case DimensionVehicleKind.Boat:
                    return ObjectID.Water;

                case DimensionVehicleKind.Minecart:
                    return ObjectID.Rail;

                default:
                    return ObjectID.None;
            }
        }

        /// <summary>What that demand is called when an author has to be told about it.</summary>
        private static string GroundItNeedsInWords(DimensionVehicleKind kind)
        {
            switch (kind)
            {
                case DimensionVehicleKind.Boat:
                    return "water";

                case DimensionVehicleKind.Minecart:
                    return "a rail";

                default:
                    return "any tile a player can walk on";
            }
        }

        public static DimensionCreatureGenerationReport Generate(
            IEnumerable<DimensionVehicleAsset> vehicles,
            string outputFolder,
            DimensionNamingContext naming)
        {
            DimensionCreatureGenerationReport report = new DimensionCreatureGenerationReport();
            if (vehicles == null || string.IsNullOrEmpty(outputFolder))
            {
                return report;
            }

            DimensionAssetFolders.Ensure(outputFolder);

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (DimensionVehicleAsset vehicle in vehicles)
                {
                    if (vehicle == null || !vehicle.Enabled || string.IsNullOrEmpty(vehicle.VehicleId))
                    {
                        continue;
                    }

                    GenerateOne(vehicle, outputFolder, naming, report);
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
            DimensionVehicleAsset vehicle,
            string outputFolder,
            DimensionNamingContext naming,
            DimensionCreatureGenerationReport report)
        {
            string stem = DimensionGeneratedPrefabUtility.SanitizeAuthoredName(vehicle.VehicleId, "Vehicle");
            string prefabPath = outputFolder + "/" + stem + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            bool updating = existing != null;

            GameObject root = updating
                ? PrefabUtility.LoadPrefabContents(prefabPath)
                : new GameObject(vehicle.VehicleId);

            try
            {
                Action<string> say = delegate(string message)
                {
                    report.Warnings.Add("'" + Describe(vehicle) + "' " + message);
                };

                // A mistyped horn hashes to a number no sound answers to, and the kart drives in
                // silence with nothing said. Checked here rather than inside Configure because
                // this is where the plain report sink is.
                DimensionSoundNames.WarnIfUnknown(
                    vehicle.HonkSoundName,
                    "'" + Describe(vehicle) + "'",
                    message => report.Warnings.Add(message));

                Configure(root, vehicle, naming, say);
                ApplyBody(root, vehicle, outputFolder, stem, say);

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
                report.Errors.Add(vehicle.VehicleId + " failed to generate: " + exception.Message);
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

        private static string Describe(DimensionVehicleAsset vehicle)
        {
            return string.IsNullOrEmpty(vehicle.DisplayName) ? vehicle.VehicleId : vehicle.DisplayName;
        }

        private static void Configure(
            GameObject root,
            DimensionVehicleAsset vehicle,
            DimensionNamingContext naming,
            Action<string> say)
        {
            ObjectAuthoring obj = EnsureComponent<ObjectAuthoring>(root);
            obj.objectName = naming.QualifyGenerated(vehicle.VehicleId);
            // The single change that turns a vehicle from scenery into a thing a player owns. Core
            // Keeper has no "vehicle" object type: all seven of its own are PlaceablePrefab, which is
            // what puts an object in a slot, in the placement UI, and back on the ground when broken.
            obj.objectType = ObjectType.PlaceablePrefab;
            obj.initialAmount = 1;

            Rarity rarity;
            if (!string.IsNullOrEmpty(vehicle.RarityId) &&
                Enum.TryParse(vehicle.RarityId, false, out rarity))
            {
                obj.rarity = rarity;
            }

            ApplyLook(root, obj, vehicle, say);
            ApplyMovement(root, vehicle, say);
            ApplyRiding(root);
            ApplyPlacement(root, vehicle, naming, say);
            ApplyBreaking(root, vehicle);

            DimensionObjectSpine.ApplyUniversal(root, false);
            DimensionObjectSpine.ApplySimpleTraits(root, vehicle.SimpleTraits, say);
            // Not paintable: no vanilla vehicle is, and a painted boat would need art the framework
            // has no way to produce. Rotatable is the author's answer, and it is the same component
            // the game's own boats and karts carry.
            DimensionObjectSpine.ApplyPlacedObject(root, false, vehicle.TurnsToFacePlacement);
            DimensionObjectSpine.ApplyBasics(root, vehicle.Basics, say);

            ApplyGhost(root);

            // The sweep, last. Same reason as every other placed thing: the answers written above
            // are each one component of a query that names several.
            DimensionQueryCompanions.FinishAWorldObject(root, vehicle.DisplayName, say);
        }

        /// <summary>Gives the vehicle a name, a tooltip, an icon and a picture in the world.</summary>
        /// <remarks>
        /// The same four places a container fills, for the same reasons. The NAME and TOOLTIP come
        /// from the mod's localization table under the qualified object name, written by
        /// <see cref="DimensionLocalizationPlan"/> against this same name. The ICON is
        /// <c>InventoryItemAuthoring.icon</c>, without which a vehicle is a blank square in the hotbar
        /// and nothing at all on the ground once broken. The PICTURE IN THE WORLD is
        /// <c>additionalSprites[0]</c>, read back at runtime by
        /// <see cref="ExpandNullforge.Vehicles.DimensionVehicleView"/>.
        /// </remarks>
        private static void ApplyLook(
            GameObject root,
            ObjectAuthoring obj,
            DimensionVehicleAsset vehicle,
            Action<string> say)
        {
            obj.additionalSprites = new List<Sprite>();
            if (vehicle.Sprite != null)
            {
                obj.additionalSprites.Add(vehicle.Sprite);
            }

            InventoryItemAuthoring inventoryItem = EnsureComponent<InventoryItemAuthoring>(root);
            inventoryItem.icon = vehicle.Icon;
            inventoryItem.smallIcon = vehicle.Icon;
            // One thing you put down, not a pile you carry — every vanilla vehicle agrees, and a
            // stackable one would let a player merge two boats into a count.
            inventoryItem.isStackable = false;
            if (inventoryItem.requiredObjectsToCraft == null)
            {
                inventoryItem.requiredObjectsToCraft =
                    new List<InventoryItemAuthoring.CraftingObject>();
            }

            if (vehicle.Icon == null)
            {
                say("has no icon and no picture, so it draws as an empty square in the inventory " +
                    "and as nothing at all where it stands. Drop a picture into its Icon or Sprite " +
                    "field.");
            }

            if (string.IsNullOrEmpty(vehicle.DisplayName))
            {
                say("has no name, so players see its raw id. Give it a name.");
            }
        }

        /// <summary>Puts on the one movement component the chosen kind uses, and takes off the rest.</summary>
        /// <remarks>
        /// Exactly one of the three, always. Leaving a boat component on a ground vehicle would make
        /// it demand water it will never find, and carrying two makes
        /// <c>TriggerUseControllableSystem</c>'s ordered check pick whichever it tests first rather
        /// than the one the author asked for.
        /// </remarks>
        private static void ApplyMovement(
            GameObject root,
            DimensionVehicleAsset vehicle,
            Action<string> say)
        {
            RemoveComponentIfPresent<BoatAuthoring>(root);
            RemoveComponentIfPresent<MinecartAuthoring>(root);
            RemoveComponentIfPresent<VehicleAuthoring>(root);

            switch (vehicle.Kind)
            {
                case DimensionVehicleKind.Boat:
                {
                    // BoatCD carries a speed multiplier and nothing else. Crossing water is not on the
                    // boat at all: BoatRidingColliderVariationPostConverter swaps the PLAYER's
                    // collider whenever they are in the BoatRiding state, so any boat inherits it.
                    EnsureComponent<BoatAuthoring>(root).speedMultiplier = vehicle.SpeedMultiplier;
                    break;
                }

                case DimensionVehicleKind.Minecart:
                {
                    MinecartAuthoring minecart = EnsureComponent<MinecartAuthoring>(root);
                    minecart.maxSpeed = vehicle.MinecartMaxSpeed;
                    // Both start at rest. currentSpeed and isBreaking are live state the riding code
                    // owns from the first tick; baking anything else in would have a freshly placed
                    // cart already rolling or already braking.
                    minecart.currentSpeed = 0f;
                    minecart.isBreaking = false;

                    if (vehicle.MinecartMaxSpeed <= 0f)
                    {
                        say("is a minecart with a top speed of zero, so it will sit on the rail and " +
                            "never move. The game's own carts use 800 and 500.");
                    }

                    break;
                }

                default:
                {
                    VehicleAuthoring kart = EnsureComponent<VehicleAuthoring>(root);
                    kart.speedMultiplier = vehicle.SpeedMultiplier;
                    kart.driftingMultiplier = vehicle.DriftingMultiplier;
                    kart.accelerationMultiplier = vehicle.AccelerationMultiplier;
                    kart.honkSound = new SFXTableIDField { value = vehicle.HonkSound };
                    break;
                }
            }

            if (vehicle.HasNumbersItsKindIgnores)
            {
                say("sets numbers its kind does not read. A boat reads only its speed, and a " +
                    "minecart only its top speed; drift, acceleration and the horn are go-kart " +
                    "numbers. Those answers change nothing.");
            }
        }

        /// <summary>
        /// The one marker that turns a placed object into a seat.
        /// </summary>
        /// <remarks>
        /// <c>CanBeControlledByOtherEntityConverter</c> bakes it into <c>ControlledByOtherEntityCD</c>,
        /// which is the component <c>TriggerUseControllableSystem</c> requires before it will hand the
        /// player a riding state, and the one <c>ControllingStateCommon</c> writes the claim into.
        /// Without it a vehicle is an object with a speed nobody can ever use. It also adds a disabled
        /// <c>IndestructibleCD</c>, which is why this is safe to put on a breakable thing.
        /// </remarks>
        private static void ApplyRiding(GameObject root)
        {
            EnsureComponent<CanBeControlledByOtherEntityAuthoring>(root);
        }

        private static void ApplyPlacement(
            GameObject root,
            DimensionVehicleAsset vehicle,
            DimensionNamingContext naming,
            Action<string> say)
        {
            PlaceableObjectAuthoring placeable = EnsureComponent<PlaceableObjectAuthoring>(root);

            // The author's own placement answers first, so the kind's rule below is the last word on
            // the fields that decide whether the vehicle can move once it is down.
            DimensionObjectSpine.ApplyPlacementRules(root, vehicle.PlacementRules, say);

            placeable.prefabTileSize = vehicle.TileSize;
            placeable.canBePlacedOnAnyWalkableTile = vehicle.Kind == DimensionVehicleKind.Ground;

            if (placeable.canBePlacedOnObjects == null)
            {
                placeable.canBePlacedOnObjects = new List<ObjectID>();
            }
            else
            {
                placeable.canBePlacedOnObjects.Clear();
            }

            ObjectID needed = GroundItNeeds(vehicle.Kind);
            if (needed != ObjectID.None)
            {
                placeable.canBePlacedOnObjects.Add(needed);
            }

            // The names go on a component of ours as well as the numbers on vanilla's, because this
            // list is the one place in the framework a runtime write cannot reach:
            // PlaceableObjectConverter puts it in the baked property blob with SetPropertyList
            // (ck-db\Pug.ECS.Conversion\PlaceableObjectConverter.cs:70), and the blob is sealed by
            // FinalizeProperties and read by the Burst-compiled PlacementHandler. The names are
            // resolved at CONVERSION time instead — see DimensionPlacedOnNamesAuthoring.
            List<string> placedOnNames = new List<string>();
            if (needed != ObjectID.None)
            {
                placedOnNames.Add(needed.ToString());
            }

            string[] extras = vehicle.AlsoGoesOnItemIds;
            bool anyOfOurOwn = false;
            for (int i = 0; i < extras.Length; i++)
            {
                if (string.IsNullOrEmpty(extras[i]))
                {
                    continue;
                }

                ObjectID extra = ResolveObject(extras[i]);
                if (extra == ObjectID.None)
                {
                    if (!naming.Owns(extras[i]))
                    {
                        say("may also be put down on '" + extras[i] + "', which is not a registered " +
                            "object. That part of the rule was left out; it still goes on " +
                            GroundItNeedsInWords(vehicle.Kind) + ".");
                        continue;
                    }

                    // One of the mod's own blocks. It has no number yet, so only the name is kept.
                    anyOfOurOwn = true;
                    placedOnNames.Add(naming.QualifyReference(extras[i]));
                    continue;
                }

                if (!placeable.canBePlacedOnObjects.Contains(extra))
                {
                    placeable.canBePlacedOnObjects.Add(extra);
                }

                placedOnNames.Add(extra.ToString());
            }

            if (anyOfOurOwn)
            {
                ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring names =
                    EnsureComponent<ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring>(root);
                names.canBePlacedOnNames = placedOnNames.ToArray();
            }
            else
            {
                // A vehicle that used to name one of the mod's blocks and no longer does must not
                // keep a component that would overwrite the list vanilla just wrote — but the
                // component is SHARED with the object-rules pass's "stays alive on" tile names, so
                // only this pass's own field is cleared, and the component goes only when nothing
                // is left on it. DimensionObjectSpine.ClearPlacedOnTileNames is the mirror of this
                // for the other field; taking the whole component off here would delete the other
                // pass's answer the moment a vehicle is ever given object rules.
                ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring names =
                    root.GetComponent<ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring>();
                if (names != null)
                {
                    names.canBePlacedOnNames = new string[0];
                    if (names.allowedTileObjectNames == null ||
                        names.allowedTileObjectNames.Length == 0)
                    {
                        RemoveComponentIfPresent<
                            ExpandNullforge.Objects.DimensionPlacedOnNamesAuthoring>(root);
                    }
                }
            }

            if (vehicle.TurnsToFacePlacement)
            {
                // RotationAuthoring rather than DirectionBasedOnVariationAuthoring, because that is
                // what the game's own boats and go-karts carry: a vehicle's facing is a live rotation
                // the riding code turns, not a variation index frozen at placement.
                EnsureComponent<RotationAuthoring>(root);
                RemoveComponentIfPresent<DirectionBasedOnVariationAuthoring>(root);
            }
            else
            {
                RemoveComponentIfPresent<RotationAuthoring>(root);
                RemoveComponentIfPresent<DirectionBasedOnVariationAuthoring>(root);
            }
        }

        /// <summary>Decides whether it can be broken, by what, and in how many hits.</summary>
        /// <remarks>
        /// The game's own set for a vehicle, which is also its set for a chest: <c>MineableAuthoring</c>
        /// plus <c>HealthAuthoring</c> plus <c>DamageReductionAuthoring</c> plus the four state
        /// components that make it flash when hit and die when emptied of health. Indestructible is
        /// expressed by adding the marker AND removing the damage components, because a thing that
        /// cannot be destroyed but still flashes when hit reads as a bug.
        /// </remarks>
        private static void ApplyBreaking(GameObject root, DimensionVehicleAsset vehicle)
        {
            if (vehicle.Indestructible)
            {
                EnsureComponent<IndestructibleAuthoring>(root);
                RemoveComponentIfPresent<DamageReductionAuthoring>(root);
                RemoveComponentIfPresent<HealthAuthoring>(root);
                RemoveComponentIfPresent<MineableAuthoring>(root);
                RemoveComponentIfPresent<TookDamageStateAuthoring>(root);
                RemoveComponentIfPresent<DeathStateAuthoring>(root);
                return;
            }

            RemoveComponentIfPresent<IndestructibleAuthoring>(root);
            EnsureComponent<MineableAuthoring>(root);
            DimensionObjectSpine.ApplyDamageableStates(root);

            // Hits-to-break IS health, because damage is capped at one point per hit just below.
            HealthAuthoring health = EnsureComponent<HealthAuthoring>(root);
            health.dontCalculateHealthFromLevel = true;
            health.maxHealth = vehicle.HitsToBreak;
            health.startHealth = vehicle.HitsToBreak;
            health.maxHealthMultiplier = 1f;

            DamageReductionAuthoring reduction = EnsureComponent<DamageReductionAuthoring>(root);
            reduction.calculateReductionFromLevel = false;
            reduction.reduction = vehicle.RequiredMiningDamage;
            reduction.reductionMultiplier = 1f;
            reduction.maxDamagePerHit = DamagePerHit;
            reduction.minDamagePerHit = 0;

            if (vehicle.DropsItselfWhenBroken)
            {
                RemoveComponentIfPresent<DontDropSelfAuthoring>(root);
            }
            else
            {
                EnsureComponent<DontDropSelfAuthoring>(root);
            }
        }

        /// <summary>
        /// Makes the vehicle exist for everybody in the game, not just whoever placed it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NOT DECORATION, AND NOT COPIED FROM VANILLA JUST BECAUSE VANILLA HAS IT. The riding claim
        /// is serialised BY GHOST ID: <c>ControllingOtherEntityCDGhostComponentSerializer</c> looks
        /// the controlled entity up in <c>GhostFromEntity</c> and, when it is not a ghost, writes
        /// zero — which every client reads back as <c>Entity.Null</c>. A vehicle without this
        /// component is therefore one only the server ever sees anybody sitting on, which in a game
        /// that runs a client world beside a server world even in single player means the rider and
        /// the vehicle disagree about whether the ride is happening at all.
        /// </para>
        /// <para>
        /// Most generated objects in this framework ship without a ghost and are right to — they do
        /// not change while anyone looks at them. A vehicle is the exception, which is why it is
        /// added here rather than in the shared spine. The settings are the ones all seven vanilla
        /// vehicles author: interpolated by default, both modes supported, no owner.
        /// </para>
        /// </remarks>
        private static void ApplyGhost(GameObject root)
        {
            Unity.NetCode.GhostAuthoringComponent ghost =
                EnsureComponent<Unity.NetCode.GhostAuthoringComponent>(root);
            ghost.DefaultGhostMode = Unity.NetCode.GhostMode.Interpolated;
            ghost.SupportedGhostModes = Unity.NetCode.GhostModeMask.All;
            ghost.HasOwner = false;
        }

        /// <summary>
        /// Writes the body a player walks up to, and points the entity at it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// TWO THINGS HAVE TO BE TRUE OF THIS PREFAB OR THE BAKE FALLS OVER, and both are easy to miss.
        /// <c>InteractablePostConverter</c> runs on anything carrying a <c>TriggerUseInteractionBuffer</c>
        /// — which the vehicle converters add unconditionally — and it reads
        /// <c>GetComponentsInChildren&lt;InteractableObject&gt;()[0]</c> without checking the array is
        /// non-empty, and <c>graphicalPrefab.GetComponent&lt;MonoBehaviour&gt;()</c> without checking
        /// the result is non-null. A vehicle body with no interactable, or with nothing on its root,
        /// is a null reference during conversion rather than a vehicle nobody can ride.
        /// </para>
        /// <para>
        /// THE INTERACTION POINT IS A CHILD TRANSFORM, NOT A NUMBER. The converter rotates every point
        /// through all four facings to build the offsets a player is measured against; an empty list
        /// bakes a single zero offset, which puts the only place you can stand exactly where the
        /// vehicle is.
        /// </para>
        /// </remarks>
        private static void ApplyBody(
            GameObject root,
            DimensionVehicleAsset vehicle,
            string outputFolder,
            string stem,
            Action<string> say)
        {
            string path = outputFolder.TrimEnd('/') + "/" + stem + "Body.prefab";
            GameObject body = new GameObject(stem + "Body");

            try
            {
                // Our own view subclass, never the game's Boat/GoKart/Minecart: those pool by
                // component type and the game claims its own pools first, and they dereference a
                // hand-built particle hierarchy every frame. See DimensionVehicleView.
                ExpandNullforge.Vehicles.DimensionVehicleView view =
                    body.AddComponent<ExpandNullforge.Vehicles.DimensionVehicleView>();

                GameObject picture = new GameObject("Body");
                picture.transform.SetParent(body.transform, false);
                SpriteRenderer renderer = picture.AddComponent<SpriteRenderer>();
                renderer.sprite = vehicle.Sprite;
                renderer.color = Color.white;
                renderer.drawMode = SpriteDrawMode.Simple;
                renderer.spriteSortPoint = SpriteSortPoint.Center;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.enabled = vehicle.Sprite != null;
                view.body = renderer;

                GameObject interactableObject = new GameObject("Interactable");
                interactableObject.transform.SetParent(body.transform, false);

                InteractableObject interactable =
                    interactableObject.AddComponent<InteractableObject>();
                interactable.radius = vehicle.HowCloseToGetOn;
                interactable.ignorePlayerDirection = vehicle.GetOnFromAnySide;
                interactable.allowToUseOnlyWhenClaimed = false;
                interactable.weightMultiplier = 1f;
                interactable.useDiscreteOutlineColor = false;
                interactable.additionalOutlineControllers = new List<OutlineController>();
                interactable.spriteObjects = new List<Pug.Sprite.SpriteObject>();

                Transform point = new GameObject("InteractionPoint").transform;
                point.SetParent(interactableObject.transform, false);
                interactable.interactingPoints = new List<Transform> { point };

                // CreateGraphicalObjectSystem only fills InteractableObjectReferenceCD from the view's
                // own field, so without this the vehicle has no outline and no use prompt even though
                // the ECS side would still let a player mount it.
                view.interactable = interactable;

                // NOT wired as a UnityEvent, and not given LocalInteractableAuthoring. Getting on is
                // decided by TriggerUseControllableSystem from the components on the entity; adding
                // LocalInteractableAuthoring here would make LocalInteractableConverter count zero
                // listeners and log "No local interaction events registered on entity" on every bake.
                GameObject written = SaveDuringABatch(body, path);
                if (written == null)
                {
                    say("could not have the body a player walks up to written to '" + path +
                        "', so nobody could get on it. Nothing was placed.");
                    return;
                }

                ObjectAuthoring objectAuthoring = root.GetComponent<ObjectAuthoring>();
                if (objectAuthoring != null)
                {
                    objectAuthoring.graphicalPrefab = written;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(body);
            }
        }

        /// <summary>
        /// Writes a brand-new prefab asset from inside the generator's asset-editing batch.
        /// </summary>
        /// <remarks>
        /// The same pause <see cref="DimensionInteractionVisualUtility"/> needs and for the same
        /// reason: inside <c>StartAssetEditing</c> the file is written but <c>SaveAsPrefabAsset</c>
        /// hands back null, because the asset has not been imported yet. Here the returned reference
        /// IS the point — it is what goes into <c>graphicalPrefab</c> — so the batch is paused for
        /// exactly this save and started again straight after, leaving the caller's own
        /// <c>StopAssetEditing</c> still closing the window it opened.
        /// </remarks>
        private static GameObject SaveDuringABatch(GameObject root, string path)
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

        /// <summary>Resolves a name to one of the GAME's own numbers, and nothing else.</summary>
        /// <remarks>
        /// THE RULE, not the history: generation time is before the game runs, so the only names
        /// with numbers are the game's own. A name of this mod's comes back <c>None</c> here, and
        /// whether that <c>None</c> means "ours, the runtime will fill it in" or "nothing answers to
        /// this" is asked of the naming context, next to the call. That is why this takes the name
        /// and nothing else.
        ///
        /// This was the sixth copy of the resolver and the last one still holding the full body.
        /// The half of it that asked <c>API.Authoring.GetObjectID</c> — twice, once qualified —
        /// could never answer: that lookup is a runtime dictionary and is empty here, so it read as
        /// though it covered the mod's own objects and always returned <c>None</c>.
        /// </remarks>
        private static ObjectID ResolveObject(string itemId)
        {
            return DimensionObjectBinder.Vanilla(itemId);
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
            // Routed through the one dependency-aware removal, so a RequireComponent cannot silently
            // defeat authoritative generation. See DimensionObjectSpine.TryRemoveComponent.
            DimensionObjectSpine.TryRemoveComponent<T>(root);
        }

    }
}
