using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the two worst components the field audit found: <c>ChaseStateAuthoring</c> at 2 of 19
    /// and <c>MeleeAttackStateAuthoring</c> at 12 of 29.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The multiplier tests are the important ones. <c>MeleeAttackStateConverter</c> throws the
    /// authored <c>meleeDamage</c> away when the object carries a tier and recomputes it from
    /// <c>LevelToDamage(level, meleeDamageMultiplier)</c>, so on any tiered creature the multiplier
    /// is the only melee damage dial there is — and it was unreachable.
    /// </para>
    /// <para>
    /// The all-or-nothing pursuit test guards the opposite failure: several vanilla behaviours bring
    /// a filled-in chase with them, and a framework that wrote a blank template over one would take
    /// a working creature and stop it doing anything when it arrived.
    /// </para>
    /// </remarks>
    public sealed class DimensionPursuitAndMeleeShapeTests
    {
        private const string TestRoot = "Assets/NullforgePursuitTests";

        [SetUp]
        public void Setup()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgePursuitTests");
            }
        }

        [TearDown]
        public void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private static DimensionCreatureStatsTemplate Stats()
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject holder = new SerializedObject(host);
            holder.FindProperty("creatureStats").FindPropertyRelative("maxHealth").intValue = 200;
            holder.ApplyModifiedPropertiesWithoutUndo();
            DimensionCreatureStatsTemplate stats = host.CreatureStats;
            Object.DestroyImmediate(host);
            return stats;
        }

        private static DimensionCreatureCombatTemplate Combat(
            System.Action<SerializedProperty> write)
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject serialized = new SerializedObject(host);
            write(serialized.FindProperty("combat"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            DimensionCreatureCombatTemplate built = host.Combat;
            Object.DestroyImmediate(host);
            return built;
        }

        private static GameObject Generate(
            string id,
            DimensionCreatureCombatTemplate combat,
            out DimensionCreatureGenerationReport report)
        {
            report = DimensionCreatureGenerator.Generate(
                new List<DimensionCreatureGenerator.Request>
                {
                    new DimensionCreatureGenerator.Request
                    {
                        CreatureId = id,
                        DisplayName = id,
                        Stats = Stats(),
                        IsEnemy = true,
                        Combat = combat
                    }
                },
                TestRoot,
                default(DimensionNamingContext));

            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/" + id + ".prefab");
        }

        // ---- pursuit ----

        [Test]
        public void ACreatureThatHangsBackGetsAStandoffRange()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "standoff",
                Combat(delegate(SerializedProperty combat)
                {
                    SerializedProperty pursuit = combat.FindPropertyRelative("pursuit");
                    pursuit.FindPropertyRelative("keepsAtLeastThisFarAway").floatValue = 4f;
                    pursuit.FindPropertyRelative("andAtMostThisFarAway").floatValue = 7f;
                    pursuit.FindPropertyRelative("neverGivesUp").boolValue = true;
                    pursuit.FindPropertyRelative("prefersPathfinding").boolValue = true;
                }),
                out report);

            ChaseStateAuthoring chase = prefab.GetComponent<ChaseStateAuthoring>();
            Assert.IsNotNull(
                chase,
                "authoring a standoff range must be enough on its own to make it chase");
            Assert.AreEqual(4f, chase.minDistanceToKeep);
            Assert.AreEqual(7f, chase.maxDistanceToKeep);
            Assert.IsTrue(chase.neverStopChasing);
            Assert.IsTrue(chase.preferPathFind);
        }

        [Test]
        public void AnUntouchedPursuitSectionAddsNoChaseAtAll()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "nochase",
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("detectionRadius").floatValue = 10f;
                }),
                out report);

            Assert.IsNull(
                prefab.GetComponent<ChaseStateAuthoring>(),
                "a blank pursuit section must not write itself over a behaviour's own chase");
        }

        [Test]
        public void ABackwardsStandoffRangeIsReportedAndReadAsOneDistance()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "backwards",
                Combat(delegate(SerializedProperty combat)
                {
                    SerializedProperty pursuit = combat.FindPropertyRelative("pursuit");
                    pursuit.FindPropertyRelative("keepsAtLeastThisFarAway").floatValue = 9f;
                    pursuit.FindPropertyRelative("andAtMostThisFarAway").floatValue = 3f;
                }),
                out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("inside out")));
            ChaseStateAuthoring chase = prefab.GetComponent<ChaseStateAuthoring>();
            Assert.AreEqual(
                9f,
                chase.maxDistanceToKeep,
                "an inverted range must not reach the game as one, or it drifts outward forever");
        }

        [Test]
        public void NeedingAPathAndNeverGivingUpIsReported()
        {
            DimensionCreatureGenerationReport report;
            Generate(
                "forever",
                Combat(delegate(SerializedProperty combat)
                {
                    SerializedProperty pursuit = combat.FindPropertyRelative("pursuit");
                    pursuit.FindPropertyRelative("needsAPathToChase").boolValue = true;
                    pursuit.FindPropertyRelative("neverGivesUp").boolValue = true;
                }),
                out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("cannot arrive")));
        }

        // ---- the melee multipliers, the dial a tier does not overwrite ----

        [Test]
        public void TheMeleeMultipliersReachTheComponent()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "hardhitter",
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("meleeDamage").intValue = 10;
                    SerializedProperty shape = combat.FindPropertyRelative("meleeShape");
                    shape.FindPropertyRelative("hitsThisHardForItsTier").floatValue = 1.5f;
                    shape.FindPropertyRelative("breaksTerrainThisHardForItsTier").floatValue = 999f;
                    shape.FindPropertyRelative("lungeForce").floatValue = 15f;
                    shape.FindPropertyRelative("damageLandsAfter").floatValue = 0.05f;
                }),
                out report);

            MeleeAttackStateAuthoring melee = prefab.GetComponent<MeleeAttackStateAuthoring>();
            Assert.IsNotNull(melee);
            Assert.AreEqual(
                1.5f,
                melee.meleeDamageMultiplier,
                "the multiplier is the only melee damage a tiered creature actually keeps");
            Assert.AreEqual(999f, melee.tileDamageMultiplier);
            Assert.AreEqual(15f, melee.moveForceForward, "the lunge every vanilla melee creature has");
            Assert.AreEqual(0.05f, melee.durationBeforeDamageDeal);
        }

        [Test]
        public void AHalfSpecifiedHitboxIsReported()
        {
            DimensionCreatureGenerationReport report;
            Generate(
                "halfbox",
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("meleeDamage").intValue = 10;
                    combat.FindPropertyRelative("meleeShape")
                        .FindPropertyRelative("hitboxHalfLength").floatValue = 2f;
                }),
                out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("connects with nothing")));
        }

        [Test]
        public void ATieredCreatureMultipliedToZeroIsReported()
        {
            DimensionCreatureGenerationReport report;
            Generate(
                "harmless",
                Combat(delegate(SerializedProperty combat)
                {
                    // Taking damage from the tier is not a switch: it is what a melee damage of zero
                    // MEANS. DimensionCreatureCombatTemplate.MeleeDamageFromLevel is derived from
                    // meleeDamage == 0, so leaving the number blank is the request to be scaled.
                    combat.FindPropertyRelative("meleeDamage").intValue = 0;
                    combat.FindPropertyRelative("meleeShape")
                        .FindPropertyRelative("hitsThisHardForItsTier").floatValue = 0f;
                }),
                out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("hurts nothing")));
        }

        [Test]
        public void ASwingThatBreaksTilesCanLeaveSomethingBehind()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "digger",
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("meleeDamage").intValue = 10;
                    combat.FindPropertyRelative("meleeBreaksTiles").boolValue = true;
                    combat.FindPropertyRelative("meleeShape")
                        .FindPropertyRelative("spawnsOnBrokenTilesId").stringValue = "Wood";
                }),
                out report);

            MeleeAttackStateAuthoring melee = prefab.GetComponent<MeleeAttackStateAuthoring>();
            Assert.AreEqual(ObjectID.Wood, melee.objectToSpawnOnHitTiles);
        }
        // ---- the shot pattern ----

        [Test]
        public void ASpiralShooterGetsItsWholeShotPattern()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "spiraller",
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("attackKind").enumValueIndex = 2;
                    combat.FindPropertyRelative("rangedDamage").intValue = 12;
                    SerializedProperty shape = combat.FindPropertyRelative("rangedShape");
                    shape.FindPropertyRelative("shotPattern").enumValueIndex = 3;
                    shape.FindPropertyRelative("muzzleDistance").floatValue = 0.5f;
                    shape.FindPropertyRelative("aimConeDegrees").floatValue = 15f;
                    shape.FindPropertyRelative("keepsAimingWhileShooting").boolValue = true;
                    shape.FindPropertyRelative("firesAtAnyAngle").boolValue = true;
                    shape.FindPropertyRelative("shotsHitThisHardForItsTier").floatValue = 0.55f;
                    shape.FindPropertyRelative("recoveryAfterShooting").floatValue = 0.5f;
                }),
                out report);

            RangeAttackStateAuthoring ranged = prefab.GetComponent<RangeAttackStateAuthoring>();
            Assert.IsNotNull(ranged);
            Assert.AreEqual(
                ProjectileSpreadType.Spiral,
                ranged.spreadType,
                "the shot pattern enum must map straight onto the game's own");
            Assert.AreEqual(0.5f, ranged.spawnAtDistanceInfront);
            Assert.AreEqual(15f, ranged.aimDegreesMax);
            Assert.IsTrue(ranged.allowReAimingWhileShooting);
            Assert.AreEqual(ProjectileSpawnDirectionType.Free, ranged.spawnDirectionType);
            Assert.AreEqual(
                0.55f,
                ranged.damageMultiplier,
                "the multiplier is the only ranged damage a tiered creature keeps");
            Assert.AreEqual(0.5f, ranged.endDuration);
        }

        [Test]
        public void ASupportShooterCanHealItsOwnSide()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "healer",
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("attackKind").enumValueIndex = 2;
                    combat.FindPropertyRelative("rangedDamage").intValue = 5;
                    SerializedProperty shape = combat.FindPropertyRelative("rangedShape");
                    shape.FindPropertyRelative("healsItsOwnSideBy").floatValue = 0.2f;
                    shape.FindPropertyRelative("shotsFollowTheirTarget").boolValue = true;
                }),
                out report);

            RangeAttackStateAuthoring ranged = prefab.GetComponent<RangeAttackStateAuthoring>();
            Assert.AreEqual(0.2f, ranged.sameFactionHealingPercentage);
            Assert.IsTrue(ranged.projectileFollowsTarget);
        }

        [Test]
        public void ASpreadWidthOnAPatternThatNeverSpreadsIsReported()
        {
            DimensionCreatureGenerationReport report;
            Generate(
                "nospread",
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("attackKind").enumValueIndex = 2;
                    combat.FindPropertyRelative("rangedDamage").intValue = 5;
                    combat.FindPropertyRelative("rangedShape")
                        .FindPropertyRelative("widestSpreadDegrees").floatValue = 75f;
                }),
                out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("never spreads")));
        }

        [Test]
        public void HalfATargetLeadingRangeIsReported()
        {
            DimensionCreatureGenerationReport report;
            Generate(
                "halflead",
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("attackKind").enumValueIndex = 2;
                    combat.FindPropertyRelative("rangedDamage").intValue = 5;
                    combat.FindPropertyRelative("rangedShape")
                        .FindPropertyRelative("startsLeadingTargetsAt").floatValue = 2f;
                }),
                out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("lead a moving target")));
        }

        [Test]
        public void ATieredShooterMultipliedToZeroIsReported()
        {
            DimensionCreatureGenerationReport report;
            Generate(
                "harmlessshooter",
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("attackKind").enumValueIndex = 2;
                    combat.FindPropertyRelative("rangedDamage").intValue = 0;
                    combat.FindPropertyRelative("rangedShape")
                        .FindPropertyRelative("shotsHitThisHardForItsTier").floatValue = 0f;
                }),
                out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("fires and hurts nothing")));
        }
        // ---- the charge, which the generic ability questions cannot describe ----

        private static SerializedProperty FirstCharge(SerializedProperty combat)
        {
            SerializedProperty abilities = combat.FindPropertyRelative("abilities");
            abilities.arraySize = 1;
            SerializedProperty ability = abilities.GetArrayElementAtIndex(0);
            ability.FindPropertyRelative("kind").enumValueIndex = 0;
            ability.FindPropertyRelative("duration").floatValue = 1f;
            return ability;
        }

        [Test]
        public void AChargerThatSlamsAWallIsLeftOpenAfterwards()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "charger",
                Combat(delegate(SerializedProperty combat)
                {
                    SerializedProperty shape =
                        FirstCharge(combat).FindPropertyRelative("chargeShape");
                    shape.FindPropertyRelative("vulnerableFor").floatValue = 2f;
                    shape.FindPropertyRelative("timeStuckOnImpact").floatValue = 0.8f;
                    shape.FindPropertyRelative("bouncesBackThisHard").floatValue = 3f;
                    shape.FindPropertyRelative("hitsThisHardForItsTier").floatValue = 1.2f;
                }),
                out report);

            ChargeAttackStateAuthoring charge = prefab.GetComponent<ChargeAttackStateAuthoring>();
            Assert.IsNotNull(charge);
            Assert.AreEqual(
                2f,
                charge.vulnerabilityDuration,
                "the open window after a charge is what makes a charging boss a fight");
            Assert.AreEqual(0.8f, charge.collideDuration);
            Assert.AreEqual(3f, charge.reversePushback);
            Assert.AreEqual(1.2f, charge.damageMultiplier);
        }

        [Test]
        public void AChargeCanEndInASwing()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "swingcharger",
                Combat(delegate(SerializedProperty combat)
                {
                    SerializedProperty shape =
                        FirstCharge(combat).FindPropertyRelative("chargeShape");
                    shape.FindPropertyRelative("endsWithASwing").boolValue = true;
                    shape.FindPropertyRelative("swingsIfWithin").floatValue = 2.5f;
                    shape.FindPropertyRelative("endingSwingDuration").floatValue = 0.8f;
                    shape.FindPropertyRelative("canSteerMidCharge").boolValue = true;
                    shape.FindPropertyRelative("widestSteerDegrees").floatValue = 135f;
                }),
                out report);

            ChargeAttackStateAuthoring charge = prefab.GetComponent<ChargeAttackStateAuthoring>();
            Assert.IsTrue(charge.endChargeWithAttack);
            Assert.AreEqual(2.5f, charge.endChargeDistanceToAttemptAttack);
            Assert.AreEqual(0.8f, charge.endChargeAttackDuration);
            Assert.IsTrue(charge.steerTowardsTargetDuringCharge);
            Assert.AreEqual(135f, charge.steerTowardsTargetMaxAngleDeg);
        }

        [Test]
        public void AnEndingSwingNobodyTurnedOnIsReported()
        {
            DimensionCreatureGenerationReport report;
            Generate(
                "deadswing",
                Combat(delegate(SerializedProperty combat)
                {
                    SerializedProperty shape =
                        FirstCharge(combat).FindPropertyRelative("chargeShape");
                    shape.FindPropertyRelative("swingsIfWithin").floatValue = 2.5f;
                    shape.FindPropertyRelative("endingSwingDuration").floatValue = 0.8f;
                }),
                out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("never turns that attack on")));
        }
        // ---- the last fields on each component, including the ones no vanilla prefab sets ----
        //
        // A field with zero vanilla users is a field with no measured value to suggest. It is not a
        // field that does nothing, and a custom creature is exactly where someone would want the
        // thing the base game never built — so these are offered, and these tests hold them open.

        [Test]
        public void AChaseCanStartSwitchedOffForSomethingElseToTurnOn()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "dormant",
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("pursuit")
                        .FindPropertyRelative("startsSwitchedOff").boolValue = true;
                }),
                out report);

            ChaseStateAuthoring chase = prefab.GetComponent<ChaseStateAuthoring>();
            Assert.IsNotNull(chase, "switching it off still has to put the state there to switch");
            Assert.IsTrue(chase.disabled);
        }

        [Test]
        public void TheRestOfTheRangedComponentIsReachable()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "fullranged",
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("attackKind").enumValueIndex = 2;
                    combat.FindPropertyRelative("rangedDamage").intValue = 7;
                    SerializedProperty shape = combat.FindPropertyRelative("rangedShape");
                    shape.FindPropertyRelative("howLongItKeepsShooting").floatValue = 1.25f;
                    shape.FindPropertyRelative("shootsAtWhatItCannotSee").boolValue = true;
                    shape.FindPropertyRelative("onlyShootsThingsItWantsToAttack").boolValue = true;
                    shape.FindPropertyRelative("spreadStartsAtDegrees").floatValue = 30f;
                    shape.FindPropertyRelative("speedChangesWithDistance").boolValue = true;
                    shape.FindPropertyRelative("speedFromNearToFar").vector2Value =
                        new Vector2(0.5f, 2f);
                    shape.FindPropertyRelative("nearAndFarDistance").vector2Value =
                        new Vector2(2f, 12f);
                    shape.FindPropertyRelative("animationName").stringValue = "shootLong";
                    shape.FindPropertyRelative("animationPerShot").stringValue = "shootTick";
                }),
                out report);

            RangeAttackStateAuthoring ranged = prefab.GetComponent<RangeAttackStateAuthoring>();
            Assert.AreEqual(1.25f, ranged.attackDuration);
            Assert.IsTrue(ranged.skipVisibilityCheck);
            Assert.IsTrue(ranged.onlyAttackTargetsWeWantToAttack);
            Assert.AreEqual(30f, ranged.startSpreadAngleOffset);
            Assert.IsTrue(ranged.modifyBaseSpeedByTargetDistance);
            Assert.AreEqual(0.5f, ranged.minMaxBaseSpeedMultiplierByTargetDistance.x);
            Assert.AreEqual(12f, ranged.minMaxDistanceForBaseSpeedMultiplier.y);
            Assert.AreEqual("shootLong", ranged.animOverride);
            Assert.AreEqual("shootTick", ranged.animPerShot);
        }

        [Test]
        public void SpeedByDistanceLeftSwitchedOffIsReported()
        {
            DimensionCreatureGenerationReport report;
            Generate(
                "deadspeed",
                Combat(delegate(SerializedProperty combat)
                {
                    combat.FindPropertyRelative("attackKind").enumValueIndex = 2;
                    combat.FindPropertyRelative("rangedDamage").intValue = 5;
                    combat.FindPropertyRelative("rangedShape")
                        .FindPropertyRelative("nearAndFarDistance").vector2Value =
                        new Vector2(2f, 10f);
                }),
                out report);

            Assert.IsTrue(report.Warnings.Exists(w => w.Contains("all travel at one speed")));
        }

        [Test]
        public void AChargeCanTurnGraduallyRatherThanSnapping()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Generate(
                "gradual",
                Combat(delegate(SerializedProperty combat)
                {
                    SerializedProperty shape =
                        FirstCharge(combat).FindPropertyRelative("chargeShape");
                    shape.FindPropertyRelative("canSteerMidCharge").boolValue = true;
                    shape.FindPropertyRelative("steerTurn").enumValueIndex = 1;
                    shape.FindPropertyRelative("steerDegreesPerSecond").floatValue = 90f;
                    shape.FindPropertyRelative("pushesWhatItHits").floatValue = 6f;
                    shape.FindPropertyRelative("flatTerrainDamage").intValue = 40;
                }),
                out report);

            ChargeAttackStateAuthoring charge = prefab.GetComponent<ChargeAttackStateAuthoring>();
            Assert.AreEqual(6f, charge.pushback);
            Assert.AreEqual(40, charge.tileDamage);
            Assert.IsNotNull(
                charge.steerTowardsTargetChargeAttackRotateToTargetData,
                "the turn data is a two-field class, not a curve asset, so it is ours to write");
            Assert.AreEqual(
                ChargeAttackRotateToTargetType.DegreesPerSecond,
                charge.steerTowardsTargetChargeAttackRotateToTargetData
                    .chargeAttackRotateToTargetType);
            Assert.AreEqual(
                90f,
                charge.steerTowardsTargetChargeAttackRotateToTargetData.degreesPerSecond);
        }
    }
}
