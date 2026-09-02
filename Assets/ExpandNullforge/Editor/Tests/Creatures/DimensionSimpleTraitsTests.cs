using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the thirty tickbox traits, and the boss markers folded into the borrowed kits.
    /// </summary>
    /// <remarks>
    /// A tickbox trait fails in exactly two ways, and both are silent. It can fail to arrive, so the
    /// author ticked something and the game never sees it. Or it can fail to leave, so an object
    /// that was once a sprinkler keeps watering the floor after the tick is cleared. Every test here
    /// checks both directions, because only checking that ticking works would pass on a generator
    /// that never removes anything.
    /// </remarks>
    public sealed class DimensionSimpleTraitsTests
    {
        private const string TestRoot = "Assets/NullforgeTraitTests";

        [SetUp]
        public void Setup()
        {
            DimensionTestScratchFolder.Ensure(TestRoot);
        }

        [TearDown]
        public void Cleanup()
        {
            DimensionTestScratchFolder.Remove(TestRoot);
        }

        private static GameObject BuildWorldObject(
            string id,
            System.Action<SerializedObject> write,
            out DimensionWorldObjectGenerationReport report)
        {
            DimensionWorldObjectAsset asset =
                ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(asset);
                serialized.FindProperty("objectIdentifier").stringValue = id;
                serialized.FindProperty("displayName").stringValue = id;
                write(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                report = DimensionWorldObjectGenerator.Generate(
                    new List<DimensionWorldObjectAsset> { asset },
                    TestRoot,
                    default(DimensionNamingContext));

                return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/" + id + ".prefab");
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private static GameObject BuildWithTraits(
            string id,
            string[] traitsToTick,
            out DimensionWorldObjectGenerationReport report)
        {
            return BuildWorldObject(id, serialized =>
            {
                SerializedProperty traits = serialized.FindProperty("simpleTraits");
                for (int i = 0; i < traitsToTick.Length; i++)
                {
                    traits.FindPropertyRelative(traitsToTick[i]).boolValue = true;
                }
            }, out report);
        }

        // ---- they arrive ----

        [Test]
        public void TickingATraitPutsItsComponentOnTheObject()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWithTraits(
                "testtraitson",
                new[]
                {
                    "isASprinkler",
                    "isATrashCan",
                    "doesNotBlockDigging",
                    "hasNoPhysics",
                    "canBeHitWithAWeaponNotJustATool",
                    "hittingItDoesNotCountAsAHit",
                    "immuneGroundDoesNotProtectIt",
                    "itsMapMarkerGoesWhenItDies"
                },
                out report);

            Assert.IsNotNull(prefab.GetComponent<SprinklerAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<TrashCanAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<DontBlockDiggingAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<DisablePhysicsAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<AttackableWithMeleeAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<DontCountAsHitForAttackerAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<IgnoreImmuneZoneAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<DisableMapMarkerOnDeathAuthoring>());
        }

        [Test]
        public void ATraitThatNeedsAnotherComponentBringsItAlong()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWithTraits(
                "testtraitdeps",
                new[] { "isATrashCan", "wearsEquipment", "canCarryAffixes" },
                out report);

            Assert.IsNotNull(
                prefab.GetComponent<InventoryAuthoring>(),
                "a trash can with nowhere to put anything is not a trash can");
            Assert.IsNotNull(
                prefab.GetComponent<SupportsConditionsAuthoring>(),
                "affixes are conditions, so an object that carries them must support conditions");
        }

        [Test]
        public void TheTraitsThatLiveInOtherAssembliesStillArrive()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWithTraits(
                "testtraitasm",
                new[] { "movesSmoothly", "hasNoPositionOfItsOwn", "canCarryAffixes" },
                out report);

            Assert.IsNotNull(prefab.GetComponent<MotionSmoothing.Authoring.MotionSmoothingAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<Pug.Conversion.DontNeedTransformAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<Affixes.Authoring.SupportAffixesAuthoring>());
        }

        // ---- they leave ----

        [Test]
        public void AnObjectWithNoTraitsTickedCarriesNoneOfThem()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWithTraits("testtraitsoff", new string[0], out report);

            Assert.IsNull(prefab.GetComponent<SprinklerAuthoring>());
            Assert.IsNull(prefab.GetComponent<TrashCanAuthoring>());
            Assert.IsNull(prefab.GetComponent<BaitOnAPoleAuthoring>());
            Assert.IsNull(prefab.GetComponent<CherryBlossomTreeAuthoring>());
            Assert.IsNull(prefab.GetComponent<FishShoalAuthoring>());
            Assert.IsNull(prefab.GetComponent<GrowingPlantAuthoring>());
            Assert.IsNull(prefab.GetComponent<TrailAuthoring>());
            Assert.IsNull(prefab.GetComponent<SoulsAuthoring>());
            Assert.IsNull(prefab.GetComponent<PlayingInstrumentAuthoring>());
            Assert.IsNull(prefab.GetComponent<CombatantsTrackerAuthoring>());
            Assert.IsNull(prefab.GetComponent<AchievementTrackerAuthoring>());
            Assert.IsNull(prefab.GetComponent<TriggerEffectAuthoring>());
        }

        [Test]
        public void ClearingATraitTakesItsComponentBackOffAgain()
        {
            DimensionWorldObjectGenerationReport first;
            BuildWithTraits(
                "testtraitregen",
                new[] { "isASprinkler", "isBaitOnAPole", "aTriggerFlipsItsLook" },
                out first);

            DimensionWorldObjectGenerationReport second;
            GameObject prefab = BuildWithTraits("testtraitregen", new string[0], out second);

            Assert.IsNull(
                prefab.GetComponent<SprinklerAuthoring>(),
                "an object that stopped being a sprinkler must stop watering the floor");
            Assert.IsNull(prefab.GetComponent<BaitOnAPoleAuthoring>());
            Assert.IsNull(prefab.GetComponent<ChangeVariationTriggerAuthoring>());
        }

        // ---- the ones that warn ----

        [Test]
        public void ACherryTreeInAHandmadeRoomSaysItCountsTowardsNothing()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWithTraits(
                "testtraitcherry",
                new[] { "isACherryTree", "isPartOfAHandmadeRoom" },
                out report);

            Assert.IsNotNull(prefab.GetComponent<CherryBlossomTreeAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<CustomScenePrefabAuthoring>());
            Assert.IsTrue(
                report.Warnings.Exists(w => w.Contains("counts towards nothing")),
                "the game skips handmade-room trees when counting, and an author cannot see that");
        }

        [Test]
        public void ACherryTreeOnItsOwnDoesNotWarn()
        {
            DimensionWorldObjectGenerationReport report;
            BuildWithTraits("testtraitcherryok", new[] { "isACherryTree" }, out report);

            Assert.IsFalse(report.Warnings.Exists(w => w.Contains("counts towards nothing")));
        }

        // ---- the boss markers folded into the kits ----

        [Test]
        public void TheCicadaKitBringsTheCicadaMarkerWithIt()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testcicada", serialized =>
            {
                serialized
                    .FindProperty("eventTerminal")
                    .FindPropertyRelative("fightsLikeTheCicada")
                    .boolValue = true;
            }, out report);

            Assert.IsNotNull(prefab.GetComponent<GiantCicadaBossAuthoring>());
            Assert.IsNotNull(
                prefab.GetComponent<CicadaBossAuthoring>(),
                "the Cicada's own systems look for this marker, so the kit is inert without it");
        }

        [Test]
        public void AnObjectWithoutTheCicadaKitCarriesNeitherPiece()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testnocicada", serialized => { }, out report);

            Assert.IsNull(prefab.GetComponent<GiantCicadaBossAuthoring>());
            Assert.IsNull(prefab.GetComponent<CicadaBossAuthoring>());
            Assert.IsNull(prefab.GetComponent<CicadaNymphAuthoring>());
        }

        [Test]
        public void ACicadaNymphIsItsOwnThingRatherThanPartOfTheKit()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testnymph", serialized =>
            {
                serialized
                    .FindProperty("eventTerminal")
                    .FindPropertyRelative("isACicadaNymph")
                    .boolValue = true;
            }, out report);

            Assert.IsNotNull(prefab.GetComponent<CicadaNymphAuthoring>());
            Assert.IsNull(
                prefab.GetComponent<GiantCicadaBossAuthoring>(),
                "a nymph is one of the small ones, not the boss");
        }

        // ---- the game's own world placement ----

        [Test]
        public void AWorldGenerationMarkerCarriesBothWorldsAnswers()
        {
            GameObject placed = new GameObject("testplaced");
            try
            {
                DimensionWorldObjectGenerationReport report;
                GameObject prefab = BuildWorldObject("testworldgen", serialized =>
                {
                    SerializedProperty where = serialized.FindProperty("nativeWorldPlacement");
                    where.FindPropertyRelative("placesSomethingWhenAWorldIsMade").boolValue = true;
                    where.FindPropertyRelative("theThingToPlace").objectReferenceValue = placed;
                    where.FindPropertyRelative("biomeInAClassicWorld").stringValue = "Desert";
                    where.FindPropertyRelative("biomeInAFullReleaseWorld").stringValue = "Sea";
                    where.FindPropertyRelative("distanceFromTheCoreClassic").intValue = 400;
                    where.FindPropertyRelative("distanceFromTheCoreFullRelease").intValue = 900;
                    where.FindPropertyRelative("order").intValue = 7;
                }, out report);

                PugWorldGen.PugWorldGenAuthoring worldGen =
                    prefab.GetComponent<PugWorldGen.PugWorldGenAuthoring>();
                Assert.IsNotNull(worldGen);
                Assert.AreEqual(Biome.Desert, worldGen.biome.classic);
                Assert.AreEqual(Biome.Sea, worldGen.biome.fullRelease);
                Assert.AreEqual(400, worldGen.targetDistanceFromCore.classic);
                Assert.AreEqual(900, worldGen.targetDistanceFromCore.fullRelease);
                Assert.AreEqual(7, worldGen.sortIndex);
            }
            finally
            {
                Object.DestroyImmediate(placed);
            }
        }

        [Test]
        public void AWorldGenerationMarkerWithNothingToPlaceIsLeftOffAndSaysSo()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testworldgenempty", serialized =>
            {
                serialized
                    .FindProperty("nativeWorldPlacement")
                    .FindPropertyRelative("placesSomethingWhenAWorldIsMade")
                    .boolValue = true;
            }, out report);

            Assert.IsNull(
                prefab.GetComponent<PugWorldGen.PugWorldGenAuthoring>(),
                "generation that places nothing is a marker that costs time and does nothing");
            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("put nothing in the world")));
        }

        [Test]
        public void ABiomeTheGameDoesNotHaveWarnsRatherThanSilentlyMoving()
        {
            GameObject placed = new GameObject("testplaced2");
            try
            {
                DimensionWorldObjectGenerationReport report;
                BuildWorldObject("testworldgenbiome", serialized =>
                {
                    SerializedProperty where = serialized.FindProperty("nativeWorldPlacement");
                    where.FindPropertyRelative("placesSomethingWhenAWorldIsMade").boolValue = true;
                    where.FindPropertyRelative("theThingToPlace").objectReferenceValue = placed;
                    where.FindPropertyRelative("biomeInAClassicWorld").stringValue = "NotABiome";
                }, out report);

                Assert.IsTrue(report.Warnings.Exists(w => w.Contains("not a")));
            }
            finally
            {
                Object.DestroyImmediate(placed);
            }
        }

        [Test]
        public void AnExactSpotLeftAtTheOriginSaysItWouldLandOnTheCore()
        {
            GameObject placed = new GameObject("testplaced3");
            try
            {
                DimensionWorldObjectGenerationReport report;
                BuildWorldObject("testworldgencore", serialized =>
                {
                    SerializedProperty where = serialized.FindProperty("nativeWorldPlacement");
                    where.FindPropertyRelative("placesSomethingWhenAWorldIsMade").boolValue = true;
                    where.FindPropertyRelative("theThingToPlace").objectReferenceValue = placed;
                    where.FindPropertyRelative("howTheSpotIsChosen").enumValueIndex =
                        (int)DimensionUniquePlacement.AnExactSpot;
                }, out report);

                Assert.IsTrue(report.Warnings.Exists(w => w.Contains("on top of the Core")));
            }
            finally
            {
                Object.DestroyImmediate(placed);
            }
        }

        [Test]
        public void AnObjectThatPlacesNothingCarriesNoWorldGenerationAtAll()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testnoworldgen", serialized => { }, out report);

            Assert.IsNull(prefab.GetComponent<PugWorldGen.PugWorldGenAuthoring>());
        }

        // ---- what using it does ----

        [Test]
        public void AnObjectThatOpensLikeAChestGetsBothHalvesWired()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testchest", serialized =>
            {
                SerializedProperty use = serialized.FindProperty("interaction");
                use.FindPropertyRelative("whatUsingItDoes").enumValueIndex =
                    (int)DimensionUseBehaviour.OpensLikeAChest;
                use.FindPropertyRelative("reach").floatValue = 1.3f;
                use.FindPropertyRelative("worksFromAnySide").boolValue = true;
            }, out report);

            Assert.IsNotNull(
                prefab.GetComponent<Interaction.LocalInteractableAuthoring>(),
                "without this the converter never looks at the wiring at all");

            ObjectAuthoring objectAuthoring = prefab.GetComponent<ObjectAuthoring>();
            Assert.IsNotNull(objectAuthoring.graphicalPrefab, "the thing a player walks up to");

            InteractableObject interactable =
                objectAuthoring.graphicalPrefab.GetComponentInChildren<InteractableObject>(true);
            Assert.IsNotNull(interactable);
            Assert.AreEqual(1.3f, interactable.radius);
            Assert.IsTrue(interactable.ignorePlayerDirection);

            Assert.AreEqual(1, interactable.onUseActions.Count);
            Assert.AreEqual(
                1,
                interactable.onUseActions[0].GetPersistentEventCount(),
                "a use with no listener makes the converter log an error and give up");
            Assert.AreEqual("Use", interactable.onUseActions[0].GetPersistentMethodName(0));

            Assert.AreEqual(1, interactable.onTriggerExitActions.Count);
            Assert.AreEqual(
                "OnPlayerLeftChest",
                interactable.onTriggerExitActions[0].GetPersistentMethodName(0),
                "a chest a player wandered away from would otherwise stay open");

            Assert.IsNotNull(objectAuthoring.graphicalPrefab.GetComponent<Chest>());
        }

        [Test]
        public void EachKindOfUseWiresItsOwnBehaviour()
        {
            AssertUseWires(
                "testbench",
                DimensionUseBehaviour.OpensACraftingBench,
                typeof(CraftingBuilding),
                "Use",
                "OnPlayerLeftBuilding");
            AssertUseWires(
                "testcattle",
                DimensionUseBehaviour.TendedLikeAnAnimal,
                typeof(Cattle),
                "Interact",
                "OnPlayerLeft");
            AssertUseWires(
                "testnpc",
                DimensionUseBehaviour.TalkedToLikeAnNpc,
                typeof(NPC),
                "Interact",
                "OnPlayerLeft");
            // Deliberately NOT typeof(SignText): the sign is the one use that cannot derive from the
            // game's component, because SignText.UpdateSprite dereferences two atlas-backed
            // SpriteObject fields every frame and a generator can never fill them. It derives from
            // WorldLabel instead, which is all the sign UI ever asks for.
            AssertUseWires(
                "testsign",
                DimensionUseBehaviour.ReadLikeASign,
                typeof(ExpandNullforge.Objects.DimensionSignView),
                "Interact",
                "OnPlayerLeft");
        }

        private static void AssertUseWires(
            string id,
            DimensionUseBehaviour what,
            System.Type expected,
            string useMethod,
            string leaveMethod)
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject(id, serialized =>
            {
                serialized
                    .FindProperty("interaction")
                    .FindPropertyRelative("whatUsingItDoes")
                    .enumValueIndex = (int)what;
            }, out report);

            GameObject visual = prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;
            Assert.IsNotNull(visual, id + " has nothing for a player to walk up to");
            Assert.IsNotNull(visual.GetComponent(expected), id + " is missing " + expected.Name);

            InteractableObject interactable =
                visual.GetComponentInChildren<InteractableObject>(true);
            Assert.AreEqual(useMethod, interactable.onUseActions[0].GetPersistentMethodName(0));
            Assert.AreEqual(
                leaveMethod,
                interactable.onTriggerExitActions[0].GetPersistentMethodName(0));
        }

        [Test]
        public void AnObjectNobodyUsesCarriesNoInteractionWiring()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWorldObject("testnouse", serialized => { }, out report);

            Assert.IsNull(prefab.GetComponent<Interaction.LocalInteractableAuthoring>());
        }

        [Test]
        public void ChangingWhatUsingItDoesDoesNotLeaveTheOldWiringBehind()
        {
            DimensionWorldObjectGenerationReport first;
            BuildWorldObject("testreuse", serialized =>
            {
                serialized
                    .FindProperty("interaction")
                    .FindPropertyRelative("whatUsingItDoes")
                    .enumValueIndex = (int)DimensionUseBehaviour.OpensLikeAChest;
            }, out first);

            DimensionWorldObjectGenerationReport second;
            GameObject prefab = BuildWorldObject("testreuse", serialized =>
            {
                serialized
                    .FindProperty("interaction")
                    .FindPropertyRelative("whatUsingItDoes")
                    .enumValueIndex = (int)DimensionUseBehaviour.ReadLikeASign;
            }, out second);

            GameObject visual = prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;
            InteractableObject interactable =
                visual.GetComponentInChildren<InteractableObject>(true);

            Assert.IsNull(visual.GetComponent<Chest>(), "the old behaviour must not survive");
            Assert.AreEqual(1, interactable.onUseActions.Count);
            Assert.AreEqual("Interact", interactable.onUseActions[0].GetPersistentMethodName(0));
        }

        [Test]
        public void AFactionTheGameDoesNotHaveWarnsRatherThanLockingEveryoneOut()
        {
            DimensionWorldObjectGenerationReport report;
            BuildWorldObject("testfaction", serialized =>
            {
                SerializedProperty use = serialized.FindProperty("interaction");
                use.FindPropertyRelative("whatUsingItDoes").enumValueIndex =
                    (int)DimensionUseBehaviour.ReadLikeASign;
                use.FindPropertyRelative("onlyThisFactionMayUseIt").stringValue = "NotAFaction";
            }, out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("not a faction")));
        }

        [Test]
        public void SomethingNobodyCanReachSaysSo()
        {
            DimensionWorldObjectGenerationReport report;
            BuildWorldObject("testreach", serialized =>
            {
                SerializedProperty use = serialized.FindProperty("interaction");
                use.FindPropertyRelative("whatUsingItDoes").enumValueIndex =
                    (int)DimensionUseBehaviour.ReadLikeASign;
                use.FindPropertyRelative("reach").floatValue = 0f;
            }, out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("close enough")));
        }

        [Test]
        public void SomethingThatCanLiveInATankCarriesTheMarkerForIt()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWithTraits("testtankdweller", new[] { "canLiveInATank" }, out report);

            Assert.IsNotNull(
                prefab.GetComponent<ContainedMiniSim.Authoring.ContainedMiniSimElementAuthoring>(),
                "a tank looks for this marker on whatever it is populated with");
        }

        [Test]
        public void TheNetworkingAndDebugTraitsArriveAndLeaveTogether()
        {
            DimensionWorldObjectGenerationReport onReport;
            GameObject on = BuildWithTraits(
                "testnetdebug",
                new[]
                {
                    "eachPlayerGetsItsOwnBiomeSamples",
                    "eachPlayerGetsItsOwnSliceOfTheMap",
                    "smoothsItselfOnceItHasSpawned",
                    "getsACharacterIdOfItsOwn",
                    "getsAPlayerIdOfItsOwn",
                    "showItOnTheDebugMap"
                },
                out onReport);

            // THESE TWO ARE REFUSED, and refusing them is the whole point. Core Keeper keeps
            // one set of biome samples and one slice of the map per player connection and asks for
            // each by expecting exactly one to exist; a second makes that ask throw every frame,
            // and the client stops reading biomes or drawing its map for the rest of the session —
            // for the whole world, not just for the mod. The pass that found this
            // (`E:\ck mods\ck-research\query-match-census.md`, slice D, P1) could not close it, so
            // the components are not written and the author is told why instead.
            Assert.IsNull(
                on.GetComponent<ClientBiomeSamplesAuthoring>(),
                "a second set of biome samples in a world stops the map reading biomes at all");
            Assert.IsNull(
                on.GetComponent<ClientSubMapAuthoring>(),
                "a second slice of the map makes the game pick which one to draw from at random");

            Assert.IsTrue(
                onReport.Warnings.Exists(w => w.Contains("biome samples")),
                "a tick that is refused has to say so; silence is the defect.");
            Assert.IsTrue(
                onReport.Warnings.Exists(w => w.Contains("slice of the map")),
                "a tick that is refused has to say so; silence is the defect.");

            Assert.IsNotNull(on.GetComponent<ConvertToInterpolatedGhostAfterSpawnAuthoring>());
            Assert.IsNotNull(on.GetComponent<CreateCharacterGuidAuthoring>());
            Assert.IsNotNull(on.GetComponent<CreatePlayerGuidAuthoring>());

            WorldExplorerDebugTrackerAuthoring tracker =
                on.GetComponent<WorldExplorerDebugTrackerAuthoring>();
            Assert.IsNotNull(tracker);
            Assert.AreEqual(WorldExplorerDebugMarkerType.Circle, tracker.markerType);
            Assert.AreEqual(2, tracker.radius);

            DimensionWorldObjectGenerationReport offReport;
            GameObject off = BuildWithTraits("testnetdebug", new string[0], out offReport);

            Assert.IsNull(off.GetComponent<ClientBiomeSamplesAuthoring>());
            Assert.IsNull(off.GetComponent<CreatePlayerGuidAuthoring>());
            Assert.IsNull(
                off.GetComponent<WorldExplorerDebugTrackerAuthoring>(),
                "an object taken off the debug map must stop being drawn on it");
        }

        [Test]
        public void ADebugMarkWithNoSizeSaysItCannotBeSeen()
        {
            DimensionWorldObjectGenerationReport report;
            BuildWorldObject("testinvisiblemark", serialized =>
            {
                SerializedProperty traits = serialized.FindProperty("simpleTraits");
                traits.FindPropertyRelative("showItOnTheDebugMap").boolValue = true;
                traits.FindPropertyRelative("debugMapRadius").intValue = 0;
            }, out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("cannot be seen")));
        }

        [Test]
        public void AFloatingDamageNumberBringsTheGhostItNeeds()
        {
            DimensionWorldObjectGenerationReport report;
            GameObject prefab = BuildWithTraits(
                "testdamagefigure",
                new[] { "isAFloatingDamageNumber" },
                out report);

            Assert.IsNotNull(prefab.GetComponent<PugDamageAuthoring>());
            Assert.IsNotNull(
                prefab.GetComponent<Unity.NetCode.GhostAuthoringComponent>(),
                "a damage number only means anything once everyone in the game can see it");
        }
    }
}
