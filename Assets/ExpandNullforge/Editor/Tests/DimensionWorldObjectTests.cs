using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the long tail: doors, lights, beds, trophies and decoration.
    /// </summary>
    /// <remarks>
    /// The failure worth guarding hardest is the lighting one. Core Keeper has three components that
    /// all sound like "it glows" and are not interchangeable — a torch carries two of them and not the
    /// third — so a lamp authored with the wrong one glows prettily and leaves the room black, and
    /// nothing about that looks wrong in the inspector.
    /// </remarks>
    public sealed class DimensionWorldObjectTests
    {
        private const string TestRoot = "Assets/NullforgeWorldObjectTests";

        private DimensionWorldObjectAsset worldObject;

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeWorldObjectTests");
            }

            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            Set("objectIdentifier", "testobject");
            Set("displayName", "Test Object");
        }

        [TearDown]
        public void Cleanup()
        {
            if (worldObject != null)
            {
                Object.DestroyImmediate(worldObject);
            }

            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private void Set(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetBool(string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetInt(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetFloat(string field, float value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetEnum(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            // intValue, not enumValueIndex — see the container tests for why.
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private DimensionWorldObjectGenerationReport Run()
        {
            return DimensionWorldObjectGenerator.Generate(
                new List<DimensionWorldObjectAsset> { worldObject },
                TestRoot,
                default(DimensionNamingContext));
        }

        private static GameObject Load()
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/testobject.prefab");
        }

        // ---- the three lightings, which are not one thing ----

        [Test]
        public void LightingTheRoomAndGlowingAreSeparateComponents()
        {
            // A torch carries ActAsLightSourceWhenHeldInHand and TableItemLightSource, and NOT
            // GlowLight. Collapsing them would make lamps that glow and light nothing.
            SetBool("lightsTheRoomWhenPlaced", true);
            SetBool("lightsTheRoomWhenHeld", true);
            SetBool("objectItselfGlows", false);
            Run();

            GameObject prefab = Load();
            Assert.IsNotNull(prefab.GetComponent<TableItemLightSourceAuthoring>(), "placed light");
            Assert.IsNotNull(prefab.GetComponent<ActAsLightSourceWhenHeldInHandAuthoring>(), "held light");
            Assert.IsNull(
                prefab.GetComponent<GlowLightAuthoring>(),
                "Glow is a tint on the object, not a light — it must not come along for the ride.");
        }

        [Test]
        public void AGlowIsWrittenWithItsOwnColourAndStrength()
        {
            SetBool("objectItselfGlows", true);
            SetFloat("glowIntensity", 2.5f);
            Run();

            GlowLightAuthoring glow = Load().GetComponent<GlowLightAuthoring>();
            Assert.IsNotNull(glow);
            Assert.AreEqual(2.5f, glow.intensity, 0.001f);
        }

        [Test]
        public void TheHeldLightCarriesItsRange()
        {
            SetBool("lightsTheRoomWhenHeld", true);
            SetInt("heldLightRange", 12);
            Run();

            Assert.AreEqual(
                12,
                Load().GetComponent<ActAsLightSourceWhenHeldInHandAuthoring>().range);
        }

        [Test]
        public void SomethingThatGlowsButLightsNothingIsReported()
        {
            SetBool("objectItselfGlows", true);
            Assert.IsTrue(worldObject.GlowsButLightsNothing);

            Assert.IsNotEmpty(Run().Warnings, "It is the lighting mistake that looks correct.");
        }

        [Test]
        public void TurningTheLightOffRemovesIt()
        {
            SetBool("lightsTheRoomWhenPlaced", true);
            Run();
            Assert.IsNotNull(Load().GetComponent<TableItemLightSourceAuthoring>());

            SetBool("lightsTheRoomWhenPlaced", false);
            Run();
            Assert.IsNull(Load().GetComponent<TableItemLightSourceAuthoring>());
        }

        // ---- what kind of thing it is ----

        /// <remarks>
        /// Renamed from "gets the component that swaps its collider". It does not swap one: the
        /// generator writes changesColliderByVariation false on purpose, because the system that
        /// does the swapping looks the object up under a variation this framework never registers
        /// and reads a null entity when it does not find one.
        /// </remarks>
        [Test]
        public void ADoorGetsTheDoorComponent()
        {
            SetEnum("kind", (int)DimensionWorldObjectKind.Door);
            Run();

            Assert.IsNotNull(Load().GetComponent<DoorAuthoring>());
        }

        [Test]
        public void ChangingKindTakesTheOldOneOff()
        {
            SetEnum("kind", (int)DimensionWorldObjectKind.Door);
            Run();

            SetEnum("kind", (int)DimensionWorldObjectKind.Bed);
            Run();

            GameObject prefab = Load();
            Assert.IsNull(prefab.GetComponent<DoorAuthoring>(), "it should stop being a door");
            Assert.IsNotNull(prefab.GetComponent<BedAuthoring>());
        }

        [Test]
        public void ATrophySummonsTheEnemyItNames()
        {
            SetEnum("kind", (int)DimensionWorldObjectKind.Trophy);
            Set("summonsEnemyId", "Slime");
            Run();

            Assert.AreEqual(
                ObjectID.Slime,
                Load().GetComponent<TrophyAuthoring>().enemyToSpawnFromSpawnerPlatform);
        }

        [Test]
        public void ATrophyWithNothingToSummonIsReported()
        {
            SetEnum("kind", (int)DimensionWorldObjectKind.Trophy);
            Assert.IsTrue(worldObject.IsATrophyThatSummonsNothing);

            Assert.IsNotEmpty(Run().Warnings);
        }

        // ---- breaking, and not being able to ----

        [Test]
        public void SomethingThatCannotBeAttackedLosesItsDamageComponents()
        {
            SetBool("cannotBeAttacked", true);
            Run();

            GameObject prefab = Load();
            Assert.IsNotNull(prefab.GetComponent<CantBeAttackedAuthoring>());
            Assert.IsNull(prefab.GetComponent<HealthAuthoring>());
            Assert.IsNull(prefab.GetComponent<MineableAuthoring>());
        }

        [Test]
        public void SomethingUnremovableForeverIsReported()
        {
            // The same trap the container asset guards: a player places it and can never take it back.
            SetBool("cannotBeAttacked", true);
            Assert.IsTrue(worldObject.CanNeverBeRemoved);

            Assert.IsNotEmpty(Run().Warnings);
        }

        [Test]
        public void ATemporaryObjectCarriesItsLifetime()
        {
            SetFloat("disappearsAfterSeconds", 30f);
            Run();

            DestroyTimerAuthoring timer = Load().GetComponent<DestroyTimerAuthoring>();
            Assert.IsNotNull(timer);
            Assert.AreEqual(30f, timer.lifetime.GetValueForCurrentPlatform(), 0.001f);
        }

        [Test]
        public void MakingItPermanentAgainRemovesTheTimer()
        {
            SetFloat("disappearsAfterSeconds", 30f);
            Run();
            Assert.IsNotNull(Load().GetComponent<DestroyTimerAuthoring>());

            SetFloat("disappearsAfterSeconds", 0f);
            Run();
            Assert.IsNull(Load().GetComponent<DestroyTimerAuthoring>());
        }

        // ---- what it does for you ----

        /// <summary>Adds an effect through the serialized effects template.</summary>
        private void AddEffect(string effectId, int value, bool eaten = false)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            SerializedProperty list = serialized.FindProperty("effects")
                .FindPropertyRelative(eaten ? "whenEaten" : "whileEquipped");
            int index = list.arraySize;
            list.arraySize = index + 1;
            SerializedProperty entry = list.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("effectId").stringValue = effectId;
            entry.FindPropertyRelative("value").intValue = value;
            entry.FindPropertyRelative("valueMultiplier").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetEffectsBool(string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty("effects").FindPropertyRelative(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void AnEquippedEffectReachesTheGamesOwnConditionList()
        {
            AddEffect("MiningIncrease", 5);
            Run();

            GivesConditionsWhenEquippedAuthoring gives =
                Load().GetComponent<GivesConditionsWhenEquippedAuthoring>();
            Assert.IsNotNull(gives);
            Assert.AreEqual(1, gives.givesConditionsWhenEquipped.Count);
            Assert.AreEqual(ConditionID.MiningIncrease, gives.givesConditionsWhenEquipped[0].id);
            Assert.AreEqual(5, gives.givesConditionsWhenEquipped[0].value);
        }

        [Test]
        public void AuthoredEffectValuesAreKeptRatherThanDerivedFromTheLevel()
        {
            // The same trap as health and attack damage: left false with an area level present, the
            // game recomputes every value and an authored +5 silently becomes the curve's number.
            AddEffect("MiningIncrease", 5);
            Run();

            Assert.IsTrue(
                Load().GetComponent<GivesConditionsWhenEquippedAuthoring>().dontCalculateValuesFromLevel,
                "Default is the author's numbers, exactly as typed.");
        }

        [Test]
        public void AnUnknownEffectIsReportedRatherThanSilentlyDropped()
        {
            AddEffect("NotARealCondition", 5);

            DimensionWorldObjectGenerationReport report = Run();
            Assert.IsNotEmpty(report.Warnings);
        }

        [Test]
        public void RemovingEveryEffectTakesTheComponentOff()
        {
            AddEffect("MiningIncrease", 5);
            Run();
            Assert.IsNotNull(Load().GetComponent<GivesConditionsWhenEquippedAuthoring>());

            Object.DestroyImmediate(worldObject);
            worldObject = ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            Set("objectIdentifier", "testobject");
            Set("displayName", "Test Object");
            Run();

            Assert.IsNull(Load().GetComponent<GivesConditionsWhenEquippedAuthoring>());
        }

        [Test]
        public void ACooldownReachesTheComponentThatEnforcesIt()
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty("effects").FindPropertyRelative("cooldownSeconds").floatValue = 12f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Run();

            Assert.AreEqual(12f, Load().GetComponent<CooldownAuthoring>().cooldown, 0.001f);
        }

        [Test]
        public void ArmourThatProtectsNothingIsCaught()
        {
            SetEffectsBool("countsAsArmor", true);
            Assert.IsTrue(
                worldObject.Effects.IsArmorThatProtectsNothing,
                "It equips, occupies the slot, and does nothing.");
        }
        // ---- right-click ----

        private void SetSecondary(DimensionSecondaryUseKind kind)
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty("secondaryUse").FindPropertyRelative("kind").intValue = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void EquipOnRightClickIsTheOneLineMostVanillaItemsUse()
        {
            // 420 of the 509 prefabs carrying SecondaryUseAuthoring are exactly this and nothing more.
            SetSecondary(DimensionSecondaryUseKind.EquipIt);
            Run();

            Assert.AreEqual(
                SecondaryUseMechanic.Equip,
                Load().GetComponent<SecondaryUseAuthoring>().mechanic);
        }

        [Test]
        public void AChargedAttackCarriesItsTiming()
        {
            SetSecondary(DimensionSecondaryUseKind.ChargedAttack);
            SerializedObject serialized = new SerializedObject(worldObject);
            SerializedProperty s = serialized.FindProperty("secondaryUse");
            s.FindPropertyRelative("chargeTime").floatValue = 1.5f;
            s.FindPropertyRelative("chargeSteps").intValue = 3;
            s.FindPropertyRelative("extraDamageMultiplier").floatValue = 2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Run();

            SecondaryUseAuthoring use = Load().GetComponent<SecondaryUseAuthoring>();
            Assert.AreEqual(SecondaryUseMechanic.WindUp, use.mechanic);
            Assert.AreEqual(1.5f, use.windupTime, 0.001f);
            Assert.AreEqual(3, use.windupTiers);
            Assert.AreEqual(2f, use.extraDamageMultiplier, 0.001f);
        }

        [Test]
        public void AReleaseStepBeyondTheChargeIsClampedRatherThanLeftUnreleasable()
        {
            // Asking for step 5 of a 3-step charge would mean the player holds the button forever.
            SetSecondary(DimensionSecondaryUseKind.ChargedAttack);
            SerializedObject serialized = new SerializedObject(worldObject);
            SerializedProperty s = serialized.FindProperty("secondaryUse");
            s.FindPropertyRelative("chargeSteps").intValue = 3;
            s.FindPropertyRelative("minimumStepToRelease").intValue = 5;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(3, worldObject.SecondaryUse.MinimumStepToRelease);
        }

        [Test]
        public void AChargeThatGainsNothingIsRecognised()
        {
            // Charging costs time and mana; one that adds nothing is strictly worse than not charging.
            SetSecondary(DimensionSecondaryUseKind.ChargedAttack);
            Assert.IsTrue(worldObject.SecondaryUse.ChargingGainsNothing);
        }

        [Test]
        public void TurningRightClickOffRemovesTheComponent()
        {
            SetSecondary(DimensionSecondaryUseKind.ChargedAttack);
            Run();
            Assert.IsNotNull(Load().GetComponent<SecondaryUseAuthoring>());

            SetSecondary(DimensionSecondaryUseKind.Nothing);
            Run();
            Assert.IsNull(Load().GetComponent<SecondaryUseAuthoring>());
        }

        // ---- the spine, and wiring ----

        [Test]
        public void AWorldObjectCarriesThePlacedObjectSpine()
        {
            Run();
            GameObject prefab = Load();

            Assert.IsNotNull(prefab.GetComponent<AnimationAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<PlaceableObjectAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<PaintableObjectAuthoring>(), "paintable by default");
            Assert.IsNotNull(prefab.GetComponent<DeathStateAuthoring>());
        }

        [Test]
        public void APoweredDoorIsBothADoorAndPartOfTheCircuit()
        {
            // The reason wiring is a composable template rather than its own asset.
            SetEnum("kind", (int)DimensionWorldObjectKind.Door);

            SerializedObject serialized = new SerializedObject(worldObject);
            serialized.FindProperty("wiring").FindPropertyRelative("role").intValue =
                (int)DimensionWiringRole.PoweredDevice;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Run();

            GameObject prefab = Load();
            Assert.IsNotNull(prefab.GetComponent<DoorAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<Pug.Automation.ElectricityAuthoring>());
        }

        // ---- hitting and breaking it ----

        [Test]
        public void FeedbackIsRemovedWhenNothingIsAskedFor()
        {
            // Generation is authoritative: an object stripped of its feedback must not keep throwing
            // the dust it used to.
            Run();
            Assert.IsNull(Load().GetComponent<TileEffectAuthoring>());
        }

        [Test]
        public void AuthoredSoundsAndParticlesReachTheGame()
        {
            SerializedObject serialized = new SerializedObject(worldObject);
            SerializedProperty feedback = serialized.FindProperty("impactFeedback");
            feedback.FindPropertyRelative("hitSoundId").stringValue = "brokenStonePillarTakeDamage";
            feedback.FindPropertyRelative("breakSoundId").stringValue = "brokenStonePillarDeath";

            SerializedProperty bursts = feedback.FindPropertyRelative("breakParticles");
            bursts.arraySize = 1;
            SerializedProperty burst = bursts.GetArrayElementAtIndex(0);
            burst.FindPropertyRelative("puffId").stringValue = "CircleDust";
            burst.FindPropertyRelative("particleCount").intValue = 12;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Run();

            TileEffectAuthoring effect = Load().GetComponent<TileEffectAuthoring>();
            Assert.IsNotNull(effect);
            Assert.AreEqual(
                DimensionSoundNames.Hash("brokenStonePillarTakeDamage"),
                effect.sfxTableDamageId.value,
                "the name the author typed, hashed the way the game hashes it");
            Assert.AreEqual(DimensionSoundNames.Hash("brokenStonePillarDeath"), effect.sfxTableDestroyId.value);
            Assert.AreEqual(1, effect.destroyPuffs.Count);
            Assert.AreEqual(PuffID.CircleDust, effect.destroyPuffs[0].puff);
            Assert.AreEqual(12, effect.destroyPuffs[0].particleCount);
        }

        [Test]
        public void AParticleBurstTheGameDoesNotHaveIsReportedRatherThanDropped()
        {
            // A missing burst is invisible until somebody swings at the thing.
            SerializedObject serialized = new SerializedObject(worldObject);
            SerializedProperty bursts =
                serialized.FindProperty("impactFeedback").FindPropertyRelative("breakParticles");
            bursts.arraySize = 1;
            bursts.GetArrayElementAtIndex(0).FindPropertyRelative("puffId").stringValue = "NotAPuff";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionWorldObjectGenerationReport report = Run();
            Assert.IsTrue(report.Warnings.Exists(warning => warning.Contains("NotAPuff")));
            Assert.IsEmpty(Load().GetComponent<TileEffectAuthoring>().destroyPuffs);
        }

        [Test]
        public void BreakingInSilenceIsDetectableWithoutSwingingAtIt()
        {
            DimensionImpactFeedbackTemplate template = new DimensionImpactFeedbackTemplate();
            Assert.IsTrue(template.BreaksSilentlyAndInvisibly);
            Assert.IsFalse(template.HasAnyFeedback);
        }

        [Test]
        public void AWorldObjectWithNoIdIsSkippedRatherThanGeneratedNameless()
        {
            Set("objectIdentifier", string.Empty);

            DimensionWorldObjectGenerationReport report = Run();
            Assert.IsEmpty(report.Created);
            Assert.AreEqual(1, report.Skipped.Count);
        }

        [Test]
        public void GeneratingTwiceUpdatesTheSamePrefab()
        {
            Assert.AreEqual(1, Run().Created.Count);

            DimensionWorldObjectGenerationReport second = Run();
            Assert.IsEmpty(second.Created);
            Assert.AreEqual(1, second.Updated.Count);
        }
    }
}
