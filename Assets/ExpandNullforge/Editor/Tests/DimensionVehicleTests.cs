using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the vehicle generator, and in particular the links that were missing when a vehicle
    /// could not be held, placed or ridden.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tests are grouped around the four things that have to be true before a player ever sees a
    /// vehicle, because each one used to be false: it is an object they can own; it can be put down
    /// where its kind can move; they can get on it; and they can break it to get it back.
    /// </para>
    /// <para>
    /// Two of these assert the absence of something rather than its presence, which is deliberate.
    /// <c>InteractablePostConverter</c> and <c>CreateGraphicalObjectSystem</c> both fail by null
    /// reference rather than by message, so a body missing its interactable, or one carrying the
    /// game's own pooled vehicle component, is a crash at bake or a kart wearing somebody else's art
    /// — neither of which any amount of reading the generated prefab would reveal.
    /// </para>
    /// </remarks>
    public sealed class DimensionVehicleTests
    {
        private const string TestRoot = "Assets/NullforgeVehicleTests";

        private DimensionVehicleAsset vehicle;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeVehicleTests");
            }

            vehicle = ScriptableObject.CreateInstance<DimensionVehicleAsset>();
            Set("vehicleId", "testkart");
            Set("displayName", "Test Kart");
        }

        [TearDown]
        public void Cleanup()
        {
            if (vehicle != null)
            {
                Object.DestroyImmediate(vehicle);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void Set(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(vehicle);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetKind(DimensionVehicleKind kind)
        {
            SerializedObject serialized = new SerializedObject(vehicle);
            // intValue, not enumValueIndex: the ordinal position and the underlying value only agree
            // while every member is implicit, and relying on that is how a renumbered enum silently
            // starts writing the wrong member.
            serialized.FindProperty("kind").intValue = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetBool(string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(vehicle);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetInt(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(vehicle);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetFloat(string field, float value)
        {
            SerializedObject serialized = new SerializedObject(vehicle);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetObject(string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(vehicle);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private Sprite MakeSprite(string name)
        {
            Texture2D texture = new Texture2D(4, 4);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            AssetDatabase.CreateAsset(sprite, TestRoot + "/" + name + ".asset");
            return AssetDatabase.LoadAssetAtPath<Sprite>(TestRoot + "/" + name + ".asset");
        }

        private GameObject Run(out DimensionCreatureGenerationReport report)
        {
            report = DimensionVehicleGenerator.Generate(
                new List<DimensionVehicleAsset> { vehicle },
                TestRoot,
                default(DimensionNamingContext));

            string path = report.Created.Count > 0
                ? report.Created[0]
                : (report.Updated.Count > 0 ? report.Updated[0] : null);

            return path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private GameObject RunAndGetBody()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(out report);
            Assert.IsNotNull(prefab, "the vehicle prefab was not written at all");
            return prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;
        }

        private static bool Mentions(DimensionCreatureGenerationReport report, string fragment)
        {
            for (int i = 0; i < report.Warnings.Count; i++)
            {
                if (report.Warnings[i].Contains(fragment))
                {
                    return true;
                }
            }

            return false;
        }

        // ---- something a player can own ----

        [Test]
        public void AVehicleIsAnObjectAPlayerCanHoldAndPutDown()
        {
            // The single measurement that started this: a vehicle used to be NonObtainable with no
            // placement, so no player could ever hold one. All seven vanilla vehicles are
            // PlaceablePrefab (800) with a PlaceableObjectAuthoring on them.
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.AreEqual(
                ObjectType.PlaceablePrefab,
                prefab.GetComponent<ObjectAuthoring>().objectType,
                "NonObtainable keeps a vehicle out of every slot and out of the placement UI");
            Assert.IsNotNull(
                prefab.GetComponent<PlaceableObjectAuthoring>(),
                "without this a vehicle can only ever exist where a scene put it");
            Assert.IsNotNull(
                prefab.GetComponent<InventoryItemAuthoring>(),
                "the icon lives here, and without it a vehicle is a blank square in the hotbar");
        }

        [Test]
        public void TheIconAndTheWorldPictureBothFallBackToWhicheverWasGiven()
        {
            // One picture is a complete answer. An author who drops in only a world sprite should not
            // get a vehicle that is half-drawn.
            Sprite art = MakeSprite("kartart");
            SetObject("sprite", art);

            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.AreSame(art, prefab.GetComponent<InventoryItemAuthoring>().icon);
            List<Sprite> extra = prefab.GetComponent<ObjectAuthoring>().additionalSprites;
            Assert.AreEqual(1, extra.Count);
            Assert.AreSame(art, extra[0], "DimensionVehicleView reads the world picture back from here");
        }

        [Test]
        public void AVehicleWithNoPictureAtAllSaysSo()
        {
            DimensionCreatureGenerationReport report;
            Run(out report);

            Assert.IsTrue(
                Mentions(report, "empty square"),
                "an author must be told their vehicle draws as nothing rather than left to find out");
        }

        // ---- getting on ----

        [Test]
        public void GettingOnNeedsTheMarkerAndTheBodyAndBothAreThere()
        {
            // TriggerUseControllableSystem requires ControlledByOtherEntityCD beside the movement
            // component before it will hand a player a riding state, and InteractablePostConverter
            // needs an InteractableObject on the body to build the InteractableCD that makes the
            // vehicle the thing "use" is pointed at. Either one missing is a vehicle nobody can ride.
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.IsNotNull(
                prefab.GetComponent<CanBeControlledByOtherEntityAuthoring>(),
                "without the controllable marker a vehicle is an object with a speed nobody can use");

            GameObject body = prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;
            Assert.IsNotNull(body, "a vehicle with no body cannot be walked up to");
            Assert.IsNotNull(
                body.GetComponentInChildren<InteractableObject>(true),
                "InteractablePostConverter indexes [0] of this without checking, so a missing one " +
                "is a null reference during conversion rather than a vehicle nobody can ride");
        }

        [Test]
        public void TheBodyHasAMonoBehaviourOnItsRootBecauseTheConverterAssumesOne()
        {
            // InteractablePostConverter calls graphicalPrefab.GetComponent<MonoBehaviour>() and then
            // dereferences the result. A body whose root carries nothing takes down the bake.
            GameObject body = RunAndGetBody();

            Assert.IsNotNull(
                body.GetComponent<MonoBehaviour>(),
                "the converter dereferences this without a null check");
        }

        [Test]
        public void TheInteractionPointIsARealChildTransform()
        {
            // The converter rotates every interacting point through all four facings to build the
            // offsets a player is measured against. An empty list bakes one zero offset, which puts
            // the only place you can stand exactly where the vehicle is.
            GameObject body = RunAndGetBody();
            InteractableObject interactable = body.GetComponentInChildren<InteractableObject>(true);

            Assert.IsNotNull(interactable.interactingPoints);
            Assert.AreEqual(1, interactable.interactingPoints.Count);
            Assert.IsNotNull(interactable.interactingPoints[0]);
        }

        [Test]
        public void HowCloseToGetOnReachesTheInteractable()
        {
            SetFloat("howCloseToGetOn", 2.5f);
            GameObject body = RunAndGetBody();

            Assert.AreEqual(
                2.5f,
                body.GetComponentInChildren<InteractableObject>(true).radius,
                0.0001f);
        }

        [Test]
        public void TheBodyIsTheFrameworksOwnViewRatherThanTheGamesGoKart()
        {
            // Two reasons at once. Core Keeper pools graphical objects by component TYPE and its own
            // pools are built first, so a mod prefab carrying the vanilla GoKart component is handed
            // one of the GAME's karts to draw itself with. And GoKart.ManagedLateUpdate dereferences
            // four particle systems and an outline controller every frame with no null check, so it
            // would be an exception per frame rather than a vehicle.
            GameObject body = RunAndGetBody();

            Assert.IsNotNull(
                body.GetComponent<ExpandNullforge.Vehicles.DimensionVehicleView>(),
                "the body must be our own view type, or it wears a vanilla kart's art");
            Assert.IsNull(body.GetComponent<GoKart>(), "the game's kart presenter would crash on this prefab");
            Assert.IsNull(body.GetComponent<Boat>(), "the game's boat presenter would crash on this prefab");
        }

        [Test]
        public void TheViewKnowsItsOwnInteractableAndItsOwnRenderer()
        {
            // CreateGraphicalObjectSystem only fills InteractableObjectReferenceCD from the view's own
            // field, so an unwired one is a vehicle with no outline and no use prompt even though the
            // ECS side would still let a player mount it.
            SetObject("sprite", MakeSprite("wired"));
            GameObject body = RunAndGetBody();
            ExpandNullforge.Vehicles.DimensionVehicleView view =
                body.GetComponent<ExpandNullforge.Vehicles.DimensionVehicleView>();

            Assert.IsNotNull(view.interactable, "no outline and no use prompt without this");
            Assert.IsNotNull(view.body, "the view needs a renderer to point at the entity's picture");
        }

        // ---- the one movement component, and only one ----

        [Test]
        public void EachKindGetsItsOwnMovementComponentAndNoOther()
        {
            // Carrying two makes TriggerUseControllableSystem's ordered check pick whichever it tests
            // first — minecart, then boat, then kart — rather than the one the author asked for.
            SetKind(DimensionVehicleKind.Boat);
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.IsNotNull(prefab.GetComponent<BoatAuthoring>());
            Assert.IsNull(prefab.GetComponent<VehicleAuthoring>());
            Assert.IsNull(prefab.GetComponent<MinecartAuthoring>());

            SetKind(DimensionVehicleKind.Minecart);
            prefab = Run(out report);

            Assert.IsNotNull(prefab.GetComponent<MinecartAuthoring>());
            Assert.IsNull(prefab.GetComponent<BoatAuthoring>(), "a leftover boat demands water it will never find");
            Assert.IsNull(prefab.GetComponent<VehicleAuthoring>());

            SetKind(DimensionVehicleKind.Ground);
            prefab = Run(out report);

            Assert.IsNotNull(prefab.GetComponent<VehicleAuthoring>());
            Assert.IsNull(prefab.GetComponent<BoatAuthoring>());
            Assert.IsNull(prefab.GetComponent<MinecartAuthoring>());
        }

        [Test]
        public void TheKartNumbersReachTheKart()
        {
            SetFloat("speedMultiplier", 1.1f);
            SetFloat("driftingMultiplier", 1.2f);
            SetFloat("accelerationMultiplier", 1.5f);

            DimensionCreatureGenerationReport report;
            VehicleAuthoring kart = Run(out report).GetComponent<VehicleAuthoring>();

            Assert.AreEqual(1.1f, kart.speedMultiplier, 0.0001f);
            Assert.AreEqual(1.2f, kart.driftingMultiplier, 0.0001f);
            Assert.AreEqual(1.5f, kart.accelerationMultiplier, 0.0001f);
        }

        [Test]
        public void AFreshMinecartIsNeitherRollingNorBraking()
        {
            // currentSpeed and isBreaking are live state the riding code owns from the first tick.
            // Baking anything else in would have a just-placed cart already moving.
            SetKind(DimensionVehicleKind.Minecart);
            SetFloat("minecartMaxSpeed", 500f);

            DimensionCreatureGenerationReport report;
            MinecartAuthoring cart = Run(out report).GetComponent<MinecartAuthoring>();

            Assert.AreEqual(500f, cart.maxSpeed, 0.0001f);
            Assert.AreEqual(0f, cart.currentSpeed, 0.0001f);
            Assert.IsFalse(cart.isBreaking);
        }

        [Test]
        public void NumbersAKindIgnoresAreCalledOutRatherThanSilentlyDropped()
        {
            // BoatCD carries a speed multiplier and nothing else. An author who tunes a boat's drift
            // would otherwise wait for a change that can never arrive.
            SetKind(DimensionVehicleKind.Boat);
            SetFloat("driftingMultiplier", 2f);

            DimensionCreatureGenerationReport report;
            Run(out report);

            Assert.IsTrue(Mentions(report, "does not read"));
        }

        [Test]
        public void AMinecartThatCouldNeverMoveSaysSo()
        {
            SetKind(DimensionVehicleKind.Minecart);
            SetFloat("minecartMaxSpeed", 0f);

            DimensionCreatureGenerationReport report;
            Run(out report);

            Assert.IsTrue(Mentions(report, "never move"));
        }

        // ---- where each kind may be put down ----

        [Test]
        public void EachKindMayOnlyBePutWhereItCanActuallyMove()
        {
            // Transcribed from the game's prefabs: both boats carry canBePlacedOnObjects [Water] with
            // canBePlacedOnAnyWalkableTile off, both minecarts carry [Rail] with it off, and all three
            // go-karts have it on. A boat set down in a corridor is a vehicle that cannot move.
            SetKind(DimensionVehicleKind.Boat);
            DimensionCreatureGenerationReport report;
            PlaceableObjectAuthoring placeable = Run(out report).GetComponent<PlaceableObjectAuthoring>();

            Assert.IsFalse(placeable.canBePlacedOnAnyWalkableTile);
            CollectionAssert.Contains(placeable.canBePlacedOnObjects, ObjectID.Water);

            SetKind(DimensionVehicleKind.Minecart);
            placeable = Run(out report).GetComponent<PlaceableObjectAuthoring>();

            Assert.IsFalse(placeable.canBePlacedOnAnyWalkableTile);
            CollectionAssert.Contains(
                placeable.canBePlacedOnObjects,
                ObjectID.Rail,
                "a minecart off its track is a minecart that cannot move");

            SetKind(DimensionVehicleKind.Ground);
            placeable = Run(out report).GetComponent<PlaceableObjectAuthoring>();

            Assert.IsTrue(placeable.canBePlacedOnAnyWalkableTile);
        }

        [Test]
        public void ChangingKindDoesNotLeaveThePreviousKindsGroundBehind()
        {
            // Regenerating over an existing prefab is the normal case, and a stale [Rail] on a boat is
            // a placement rule for a vehicle the author already changed their mind about.
            SetKind(DimensionVehicleKind.Minecart);
            DimensionCreatureGenerationReport report;
            Run(out report);

            SetKind(DimensionVehicleKind.Boat);
            PlaceableObjectAuthoring placeable = Run(out report).GetComponent<PlaceableObjectAuthoring>();

            CollectionAssert.DoesNotContain(placeable.canBePlacedOnObjects, ObjectID.Rail);
            CollectionAssert.Contains(placeable.canBePlacedOnObjects, ObjectID.Water);
        }

        [Test]
        public void AnExtraPlaceItGoesOnThatIsNotARealObjectIsCalledOut()
        {
            SerializedObject serialized = new SerializedObject(vehicle);
            SerializedProperty extras = serialized.FindProperty("alsoGoesOnItemIds");
            extras.arraySize = 1;
            extras.GetArrayElementAtIndex(0).stringValue = "NotAnObjectAtAll";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionCreatureGenerationReport report;
            Run(out report);

            Assert.IsTrue(Mentions(report, "not a registered"));
        }

        // ---- breaking it ----

        [Test]
        public void HitsToBreakIsHealthBecauseDamageIsCappedAtOne()
        {
            // All seven vanilla vehicles author maxHealth 2 with maxDamagePerHit 1, which is where
            // "two hits with anything" comes from. Without the cap, hits-to-break would be a race
            // between health and whatever weapon the player happened to swing.
            SetInt("hitsToBreak", 3);

            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(out report);

            HealthAuthoring health = prefab.GetComponent<HealthAuthoring>();
            Assert.AreEqual(3, health.maxHealth);
            Assert.AreEqual(3, health.startHealth);
            Assert.IsTrue(
                health.dontCalculateHealthFromLevel,
                "an authored number the area level is allowed to overwrite is not an authored number");
            Assert.AreEqual(1, prefab.GetComponent<DamageReductionAuthoring>().maxDamagePerHit);
            Assert.IsNotNull(prefab.GetComponent<MineableAuthoring>());
        }

        [Test]
        public void TheDefaultIsTheGamesOwnTwoHits()
        {
            DimensionCreatureGenerationReport report;
            Assert.AreEqual(
                DimensionVehicleAsset.VanillaVehicleHitsToBreak,
                Run(out report).GetComponent<HealthAuthoring>().maxHealth);
        }

        [Test]
        public void BreakingItGivesItBackUnlessTheAuthorSaysOtherwise()
        {
            DimensionCreatureGenerationReport report;
            Assert.IsNull(
                Run(out report).GetComponent<DontDropSelfAuthoring>(),
                "a vehicle a player crafted and cannot get back is a vehicle they will not place");

            SetBool("dropsItselfWhenBroken", false);
            Assert.IsNotNull(Run(out report).GetComponent<DontDropSelfAuthoring>());
        }

        [Test]
        public void AnIndestructibleVehicleStopsFlashingWhenHitAsWellAsStopsDying()
        {
            // Leaving the damage components on something marked indestructible gives a vehicle that
            // cannot be destroyed but still reacts to every hit, which reads as a bug.
            SetBool("indestructible", true);

            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.IsNotNull(prefab.GetComponent<IndestructibleAuthoring>());
            Assert.IsNull(prefab.GetComponent<HealthAuthoring>());
            Assert.IsNull(prefab.GetComponent<MineableAuthoring>());
            Assert.IsNull(prefab.GetComponent<TookDamageStateAuthoring>());
            Assert.IsNull(prefab.GetComponent<DeathStateAuthoring>());
        }

        [Test]
        public void GoingBackToBreakableTakesTheIndestructibleMarkerOffAgain()
        {
            SetBool("indestructible", true);
            DimensionCreatureGenerationReport report;
            Run(out report);

            SetBool("indestructible", false);
            GameObject prefab = Run(out report);

            Assert.IsNull(prefab.GetComponent<IndestructibleAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<HealthAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<DeathStateAuthoring>());
        }

        // ---- the rest of the spine ----

        [Test]
        public void AVehicleIsSentOverTheNetwork()
        {
            // Both halves of the riding claim are replicated, and a vehicle's state changes every tick
            // while somebody is on it. All seven vanilla vehicles carry a ghost.
            DimensionCreatureGenerationReport report;
            Assert.IsNotNull(Run(out report).GetComponent<Unity.NetCode.GhostAuthoringComponent>());
        }

        [Test]
        public void FacingIsALiveRotationRatherThanAFrozenVariation()
        {
            // The game's own boats and karts carry RotationAuthoring, not
            // DirectionBasedOnVariationAuthoring: a vehicle's facing is turned by the riding code
            // every tick, not chosen once at placement.
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(out report);

            Assert.IsNotNull(prefab.GetComponent<RotationAuthoring>());
            Assert.IsNull(prefab.GetComponent<DirectionBasedOnVariationAuthoring>());

            SetBool("turnsToFacePlacement", false);
            Assert.IsNull(Run(out report).GetComponent<RotationAuthoring>());
        }

        [Test]
        public void ADisabledVehicleGeneratesNothing()
        {
            SetBool("enabled", false);

            DimensionCreatureGenerationReport report;
            Run(out report);

            Assert.AreEqual(0, report.Created.Count);
            Assert.AreEqual(0, report.Updated.Count);
        }

        [Test]
        public void ANameGetsARowThePlayerCanActuallyRead()
        {
            // This used to be a warning saying no row could be written, because a vehicle had no slot
            // for a name to appear in. It has one now, so it is named like every other placeable.
            DimensionTemplateAsset template = ScriptableObject.CreateInstance<DimensionTemplateAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(template);
                SerializedProperty list = serialized.FindProperty("globalVehicles");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = vehicle;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DimensionLocalizationPlan plan =
                    DimensionLocalizationPlan.Build(template, default(DimensionNamingContext));

                bool named = false;
                for (int i = 0; i < plan.Rows.Count; i++)
                {
                    if (plan.Rows[i].EnglishText == "Test Kart")
                    {
                        named = true;
                    }
                }

                Assert.IsTrue(named, "a vehicle with a name must reach the localization table");
            }
            finally
            {
                Object.DestroyImmediate(template);
            }
        }
    }
}
