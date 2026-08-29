using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Guards the promise that a creature's numbers are the author's numbers.
    /// </summary>
    /// <remarks>
    /// Core Keeper recalculates health and damage reduction from the area level in <c>OnValidate</c>,
    /// which runs whenever a prefab is touched. So an authored 450 health does not fail loudly if the
    /// derivation is left on — it survives generation, and then silently becomes whatever the curve
    /// says the next time anything opens the asset. That is the failure these tests exist for.
    /// </remarks>
    public sealed class DimensionCreatureGeneratorTests
    {
        private const string TestRoot = "Assets/NullforgeCreatureTests";

        [SetUp]
        public void CreateScratchFolder()
        {
            if (!AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.CreateFolder("Assets", "NullforgeCreatureTests");
            }
        }

        [TearDown]
        public void RemoveScratchFolder()
        {
            if (AssetDatabase.IsValidFolder(TestRoot))
            {
                AssetDatabase.DeleteAsset(TestRoot);
            }
        }

        private static DimensionCreatureStatsTemplate Stats(
            DimensionCreatureStatSource source,
            int health,
            int reduction)
        {
            DimensionCreatureStatsTemplate stats = new DimensionCreatureStatsTemplate();
            SerializedObject holder = null;

            // The template is a plain serializable class, so the fields are set through a host object
            // the same way the authoring assets set them.
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            holder = new SerializedObject(host);
            SerializedProperty root = holder.FindProperty("creatureStats");
            root.FindPropertyRelative("statSource").enumValueIndex = (int)source;
            root.FindPropertyRelative("maxHealth").intValue = health;
            root.FindPropertyRelative("damageReduction").intValue = reduction;
            holder.ApplyModifiedPropertiesWithoutUndo();

            stats = host.CreatureStats;
            return stats;
        }

        private static DimensionCreatureGenerator.Request Request(
            DimensionCreatureStatsTemplate stats,
            bool isBoss = false,
            string behaviour = "")
        {
            return new DimensionCreatureGenerator.Request
            {
                CreatureId = "testcreature",
                DisplayName = "Test Creature",
                Stats = stats,
                BehaviourName = behaviour,
                IsEnemy = true,
                IsBoss = isBoss,
                Enabled = true
            };
        }

        private static GameObject Run(DimensionCreatureGenerator.Request request, out DimensionCreatureGenerationReport report)
        {
            report = DimensionCreatureGenerator.Generate(
                new List<DimensionCreatureGenerator.Request> { request },
                TestRoot,
                default(DimensionNamingContext));

            // Both lists, not just Created: the second generate in a test UPDATES the prefab rather
            // than creating it, and reading only Created hands the test a null to dereference.
            string path = report.Created.Count > 0
                ? report.Created[0]
                : (report.Updated.Count > 0 ? report.Updated[0] : null);

            return path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>Builds a combat template through a host asset, the way Stats does.</summary>
        private static DimensionCreatureCombatTemplate Combat(
            DimensionCreatureAttackKind attackKind,
            DimensionCreatureIdleMovement idle = DimensionCreatureIdleMovement.WanderNearby,
            string projectile = "",
            string faction = "OnlyAttacksPlayer",
            float detectionRadius = 12f,
            float rangedMinDistance = 2f)
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject holder = new SerializedObject(host);
            SerializedProperty root = holder.FindProperty("combat");

            // intValue, not enumValueIndex — the ordinal and the value diverge the moment an enum
            // member is given an explicit number, and the write then silently does nothing.
            root.FindPropertyRelative("attackKind").intValue = (int)attackKind;
            root.FindPropertyRelative("idleMovement").intValue = (int)idle;
            root.FindPropertyRelative("projectileItemId").stringValue = projectile;
            root.FindPropertyRelative("factionId").stringValue = faction;
            root.FindPropertyRelative("detectionRadius").floatValue = detectionRadius;
            root.FindPropertyRelative("rangedMinDistance").floatValue = rangedMinDistance;
            holder.ApplyModifiedPropertiesWithoutUndo();

            return host.Combat;
        }

        private static DimensionCreatureGenerator.Request CombatRequest(DimensionCreatureCombatTemplate combat)
        {
            DimensionCreatureGenerator.Request request =
                Request(Stats(DimensionCreatureStatSource.Authored, 100, 0));
            request.Combat = combat;
            return request;
        }

        /// <summary>Builds a combat template carrying one ability.</summary>
        private static DimensionCreatureCombatTemplate CombatWithAbility(
            DimensionCreatureAbilityKind kind,
            string targetObjectId = "",
            int power = 0,
            int extraOfSameKind = 0)
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject holder = new SerializedObject(host);
            SerializedProperty root = holder.FindProperty("combat");
            root.FindPropertyRelative("attackKind").intValue = (int)DimensionCreatureAttackKind.Melee;
            root.FindPropertyRelative("factionId").stringValue = "OnlyAttacksPlayer";

            SerializedProperty list = root.FindPropertyRelative("abilities");
            list.arraySize = 1 + extraOfSameKind;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("kind").intValue = (int)kind;
                entry.FindPropertyRelative("targetObjectId").stringValue = targetObjectId;
                entry.FindPropertyRelative("power").intValue = power;
            }

            holder.ApplyModifiedPropertiesWithoutUndo();
            return host.Combat;
        }

        // ---- abilities ----

        [Test]
        public void AnAbilityBecomesTheComponentThatImplementsIt()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(
                CombatRequest(CombatWithAbility(DimensionCreatureAbilityKind.ChargeAttack)),
                out report);

            Assert.IsNotNull(prefab, string.Join("; ", report.Errors));
            Assert.IsNotNull(prefab.GetComponent<ChargeAttackStateAuthoring>());
        }

        [Test]
        public void DroppingAnAbilityTakesItOffTheCreature()
        {
            // Add-only, a boss accumulates every ability it was ever given across edits, with nothing
            // in the asset still describing them.
            DimensionCreatureGenerationReport report;
            Run(CombatRequest(CombatWithAbility(DimensionCreatureAbilityKind.Teleport)), out report);

            GameObject prefab = Run(
                CombatRequest(CombatWithAbility(DimensionCreatureAbilityKind.Enrage)),
                out report);

            Assert.IsNull(prefab.GetComponent<TeleportStateAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<EnrageStateAuthoring>());
        }

        [Test]
        public void AnExplodingCreatureDoesNotBlowUpTheInstantItSpawns()
        {
            // ExplodeStateAuthoring defaults explodeOnInitialization to TRUE, which is right for a
            // thrown bomb and catastrophic for a creature.
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(
                CombatRequest(CombatWithAbility(
                    DimensionCreatureAbilityKind.Explode,
                    targetObjectId: "Explosion")),
                out report);

            Assert.IsFalse(prefab.GetComponent<ExplodeStateAuthoring>().explodeOnInitialization);
        }

        [Test]
        public void AnAbilityNeedingAnObjectAndLackingOneIsReported()
        {
            DimensionCreatureGenerationReport report;
            Run(CombatRequest(CombatWithAbility(DimensionCreatureAbilityKind.Breed)), out report);

            Assert.IsNotEmpty(report.Warnings, "A breeder with no baby produces nothing, silently.");
        }

        [Test]
        public void ListingTheSameAbilityTwiceIsReported()
        {
            // Each is a single component, so the second entry overwrites the first and the earlier
            // settings vanish with no sign anything went wrong.
            DimensionCreatureCombatTemplate combat = CombatWithAbility(
                DimensionCreatureAbilityKind.JumpAttack,
                extraOfSameKind: 1);
            Assert.IsTrue(combat.HasDuplicateAbilities);

            DimensionCreatureGenerationReport report;
            Run(CombatRequest(combat), out report);
            Assert.IsNotEmpty(report.Warnings);
        }

        [Test]
        public void ACreatureThatOnlyChargesStillCountsAsAbleToAttack()
        {
            // No melee and no ranged, but a charge — it can hurt you, so the "chases and does nothing"
            // warning must not fire.
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject holder = new SerializedObject(host);
            SerializedProperty root = holder.FindProperty("combat");
            root.FindPropertyRelative("attackKind").intValue = (int)DimensionCreatureAttackKind.None;
            SerializedProperty list = root.FindPropertyRelative("abilities");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).FindPropertyRelative("kind").intValue =
                (int)DimensionCreatureAbilityKind.ChargeAttack;
            holder.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(host.Combat.ChasesButCannotAttack);
        }

        [Test]
        public void AbilityPowerLeftAtZeroIsNotWrittenSoTheLevelStaysInCharge()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(
                CombatRequest(CombatWithAbility(DimensionCreatureAbilityKind.JumpAttack)),
                out report);

            Assert.AreEqual(0, prefab.GetComponent<JumpAttackStateAuthoring>().jumpDamage);

            prefab = Run(
                CombatRequest(CombatWithAbility(DimensionCreatureAbilityKind.JumpAttack, power: 42)),
                out report);
            Assert.AreEqual(42, prefab.GetComponent<JumpAttackStateAuthoring>().jumpDamage);
        }

        [Test]
        public void AskingForExactNumbersKeepsAreaLevelOffSoAttackDamageSurvives()
        {
            // HealthAuthoring can opt out of level scaling; the ATTACK components cannot — their
            // OnValidate overwrites damage unconditionally whenever an AreaLevelAuthoring is present.
            // So an authored creature must not have one, or its authored damage is unkeepable.
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(
                CombatRequest(Combat(DimensionCreatureAttackKind.Melee)),
                out report);

            Assert.IsNull(
                prefab.GetComponent<AreaLevelAuthoring>(),
                "An authored creature with an area level cannot keep its own attack damage.");
        }

        [Test]
        public void TickingScalesWithItsTierCannotPutTheAreaLevelBackOnAnExactlyNumberedCreature()
        {
            // THE LATER-PASS CLOBBER, WITH BOTH BLOCKS SET AT ONCE — which is why the test above
            // could not see it and this one has to exist. ApplyStateMachine takes the area level
            // off for a creature whose numbers are kept exactly; ApplyBasics then ran four hundred
            // lines later and, on this one branch, added it straight back. With it present,
            // MeleeAttackStateAuthoring.OnValidate recomputes the attack damage from the tier and
            // offers no way out, so the number the author typed was destroyed with nothing said.
            DimensionCreatureCombatTemplate combat = CombatThatScalesWithItsTier(
                DimensionCreatureAttackKind.Melee, DimensionAreaTier.Crystal);

            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(CombatRequest(combat), out report);

            Assert.IsNull(
                prefab.GetComponent<AreaLevelAuthoring>(),
                "Ticking 'scales with its tier' put the area level back on a creature asking to " +
                "keep its own numbers, so its attack damage is recomputed from the curve.");

            bool said = false;
            for (int i = 0; i < report.Warnings.Count; i++)
            {
                if (report.Warnings[i].Contains("tier"))
                {
                    said = true;
                    break;
                }
            }

            Assert.IsTrue(
                said,
                "Two controls asked for opposite things and neither of them happened quietly. " +
                "The author has to be told which one won.");
        }

        /// <summary>A combat block that both scales with its tier and names one.</summary>
        private static DimensionCreatureCombatTemplate CombatThatScalesWithItsTier(
            DimensionCreatureAttackKind attackKind,
            DimensionAreaTier tier)
        {
            DimensionMobAsset host = ScriptableObject.CreateInstance<DimensionMobAsset>();
            SerializedObject holder = new SerializedObject(host);
            SerializedProperty root = holder.FindProperty("combat");
            root.FindPropertyRelative("attackKind").intValue = (int)attackKind;
            root.FindPropertyRelative("factionId").stringValue = "OnlyAttacksPlayer";
            root.FindPropertyRelative("detectionRadius").floatValue = 12f;

            SerializedProperty basics = root.FindPropertyRelative("basics");
            basics.FindPropertyRelative("scalesWithItsTier").boolValue = true;
            basics.FindPropertyRelative("areaTier").intValue = (int)tier;
            holder.ApplyModifiedPropertiesWithoutUndo();

            return host.Combat;
        }

        [Test]
        public void AskingForVanillaScalingPutsTheAreaLevelOn()
        {
            DimensionCreatureGenerator.Request request =
                Request(Stats(DimensionCreatureStatSource.AreaLevelCurve, 100, 0));
            request.Combat = Combat(DimensionCreatureAttackKind.Melee);

            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(request, out report);

            Assert.IsNotNull(
                prefab.GetComponent<AreaLevelAuthoring>(),
                "Scaling with the area is exactly what the area level component is for.");
        }

        // ---- drops collected from the item side ----

        /// <summary>Builds a drop bucket naming this creature as the source.</summary>
        private static DimensionDropsForSource DropsOnTestCreature(
            string itemId,
            int minAmount,
            int maxAmount)
        {
            DimensionWorldObjectAsset host =
                ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            SerializedObject holder = new SerializedObject(host);
            SerializedProperty list = holder.FindProperty("dropsFrom");
            list.arraySize = 1;
            SerializedProperty entry = list.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("kind").intValue = (int)DimensionDropSourceKind.Creature;
            entry.FindPropertyRelative("sourceId").stringValue = "testcreature";
            entry.FindPropertyRelative("chance").floatValue = 1f;
            entry.FindPropertyRelative("weight").intValue = 1;
            entry.FindPropertyRelative("minAmount").intValue = minAmount;
            entry.FindPropertyRelative("maxAmount").intValue = maxAmount;
            entry.FindPropertyRelative("enabled").boolValue = true;
            holder.ApplyModifiedPropertiesWithoutUndo();

            List<KeyValuePair<string, DimensionDropSource[]>> items =
                new List<KeyValuePair<string, DimensionDropSource[]>>
                {
                    new KeyValuePair<string, DimensionDropSource[]>(itemId, host.DropsFrom)
                };

            List<DimensionDropsForSource> grouped = DimensionDropCollector.GroupBySource(items);
            Object.DestroyImmediate(host);
            return grouped.Count > 0 ? grouped[0] : null;
        }

        [Test]
        public void AnItemThatNamedThisCreatureBecomesItsCustomLoot()
        {
            // The far end of the inversion: the author wrote it on the item, it lands on the creature.
            DimensionCreatureGenerator.Request request =
                Request(Stats(DimensionCreatureStatSource.Authored, 100, 0));
            request.DropsFromItems = DropsOnTestCreature("Wood", 1, 1);

            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(request, out report);

            DropLootAuthoring loot = prefab.GetComponent<DropLootAuthoring>();
            Assert.IsNotNull(loot, "nothing was written");
            Assert.IsTrue(loot.hasCustomLoot);
            Assert.AreEqual(ObjectID.Wood, loot.customLoot.Values[0].lootDropID);
        }

        [Test]
        public void ARangedDropIsCarriedOutForTheLootTableRatherThanFlattened()
        {
            // LootDrop has no range. Writing it as custom loot anyway would quietly turn "2 to 5"
            // into a single number, so it has to leave the generator intact.
            DimensionCreatureGenerator.Request request =
                Request(Stats(DimensionCreatureStatSource.Authored, 100, 0));
            request.DropsFromItems = DropsOnTestCreature("Wood", 2, 5);

            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(request, out report);

            Assert.AreEqual(1, report.DropsNeedingALootTable.Count, "it must not be silently dropped");
            Assert.AreEqual("Wood", report.DropsNeedingALootTable[0].ItemId);

            // AND IT HAS SOMEWHERE TO GO. The drop is registered against the creature's loot table
            // when the game loads, and the game finds that table through DropsLootFromLootTableCD,
            // which DropLootConverter writes only when the object says it has one. A creature
            // authored with no loot table asset said no, so the drop landed nowhere at all and the
            // only message read like the item name had been mistyped.
            DropLootAuthoring loot = prefab.GetComponent<DropLootAuthoring>();
            Assert.IsNotNull(loot, "the creature has no loot component to carry a table id");
            Assert.IsTrue(
                loot.hasLootTable,
                "the creature has no loot table, so there is nowhere for this drop to be added");
            Assert.AreNotEqual(LootTableID.Empty, loot.lootTableID);
            Assert.GreaterOrEqual(
                (int)loot.lootTableID,
                ExpandNullforge.Loot.DimensionLootTableRegistry.MinCustomLootTableId,
                "an auto table must never claim one of the game's own table ids");
        }

        [Test]
        public void ACreatureNothingDropsFromKeepsNoLootComponent()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(
                Request(Stats(DimensionCreatureStatSource.Authored, 100, 0)),
                out report);

            Assert.IsNull(prefab.GetComponent<DropLootAuthoring>());
            Assert.IsEmpty(report.DropsNeedingALootTable);
        }

        // ---- the state machine every vanilla creature has ----

        [Test]
        public void ACreatureGetsTheStateMachineItNeedsToActAtAll()
        {
            // StateAuthoring is the root the other states hang off, and Idle/TookDamage sit beside it
            // on 1,300+ vanilla prefabs. Only DeathState was being written before.
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(CombatRequest(Combat(DimensionCreatureAttackKind.Melee)), out report);

            Assert.IsNotNull(prefab, string.Join("; ", report.Errors));
            Assert.IsNotNull(prefab.GetComponent<StateAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<IdleStateAuthoring>());
            Assert.IsNotNull(prefab.GetComponent<TookDamageStateAuthoring>());
        }

        // ---- the attack, which was missing entirely ----

        [Test]
        public void AMeleeCreatureCanActuallyHitSomething()
        {
            // Before this the generator wrote ChaseState and no attack, so a creature ran at the
            // player and stood there. This is the regression that matters most in the whole file.
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(CombatRequest(Combat(DimensionCreatureAttackKind.Melee)), out report);

            Assert.IsNotNull(
                prefab.GetComponent<MeleeAttackStateAuthoring>(),
                "A melee creature with no melee attack state chases and never swings.");
            Assert.IsNotNull(
                prefab.GetComponent<DetectCollisionAuthoring>(),
                "Melee needs to know it touched something.");
        }

        [Test]
        public void ARangedCreatureShootsWhatItWasToldTo()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(
                CombatRequest(Combat(DimensionCreatureAttackKind.Ranged, projectile: "SlingshotProjectile")),
                out report);

            RangeAttackStateAuthoring ranged = prefab.GetComponent<RangeAttackStateAuthoring>();
            Assert.IsNotNull(ranged);
            Assert.AreEqual(ObjectID.SlingshotProjectile, ranged.projectileID);
        }

        [Test]
        public void SwitchingAwayFromAnAttackRemovesIt()
        {
            // Left behind, a creature keeps shooting with nothing in the asset still asking it to.
            DimensionCreatureGenerationReport report;
            Run(CombatRequest(Combat(DimensionCreatureAttackKind.Ranged, projectile: "SlingshotProjectile")), out report);

            GameObject prefab = Run(CombatRequest(Combat(DimensionCreatureAttackKind.Melee)), out report);
            Assert.IsNull(prefab.GetComponent<RangeAttackStateAuthoring>());

            prefab = Run(CombatRequest(Combat(DimensionCreatureAttackKind.None)), out report);
            Assert.IsNull(prefab.GetComponent<MeleeAttackStateAuthoring>());
        }

        [Test]
        public void DamageLeftAtZeroIsNotWrittenSoTheLevelCurveStaysInCharge()
        {
            // MeleeAttackStateAuthoring.OnValidate recomputes meleeDamage from the area level whenever
            // one is present, so writing a number there would be overwritten anyway.
            DimensionCreatureCombatTemplate combat = Combat(DimensionCreatureAttackKind.Melee);
            Assert.IsTrue(combat.MeleeDamageFromLevel);

            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(CombatRequest(combat), out report);

            Assert.AreEqual(0, prefab.GetComponent<MeleeAttackStateAuthoring>().meleeDamage);
        }

        // ---- noticing things, which chasing depends on ----

        [Test]
        public void ACreatureCanPerceiveAndHasASide()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(CombatRequest(Combat(DimensionCreatureAttackKind.Melee)), out report);

            Assert.IsNotNull(
                prefab.GetComponent<NearbyEntitiesTrackerAuthoring>(),
                "ChaseState with no tracker has nothing to chase.");
            Assert.AreEqual(
                FactionID.OnlyAttacksPlayer,
                prefab.GetComponent<FactionAuthoring>().faction);
            Assert.IsNotNull(
                prefab.GetComponent<SupportsConditionsAuthoring>(),
                "Without this it silently ignores poison, slow, burning and every buff.");
        }

        [Test]
        public void AnUnknownFactionIsReportedRatherThanSilentlyDropped()
        {
            DimensionCreatureGenerationReport report;
            Run(CombatRequest(Combat(DimensionCreatureAttackKind.Melee, faction: "NotAFaction")), out report);

            Assert.IsNotEmpty(report.Warnings);
        }

        // ---- wandering ----

        [Test]
        public void WanderingUsesRandomWalkWhichIsWhatVanillaActuallyUses()
        {
            // RoamingState is on four prefabs in the whole game; RandomWalkState is what enemies wander
            // with. Getting this wrong produces a creature that stands still and looks broken.
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(CombatRequest(Combat(DimensionCreatureAttackKind.Melee)), out report);
            Assert.IsNotNull(prefab.GetComponent<RandomWalkStateAuthoring>());

            prefab = Run(
                CombatRequest(Combat(
                    DimensionCreatureAttackKind.Melee,
                    idle: DimensionCreatureIdleMovement.StandStill)),
                out report);
            Assert.IsNull(
                prefab.GetComponent<RandomWalkStateAuthoring>(),
                "Turning wandering off has to remove the state, not just stop configuring it.");
        }

        // ---- the quiet mistakes ----

        [Test]
        public void AnEnemyWithNoAttackIsReported()
        {
            DimensionCreatureGenerationReport report;
            Run(CombatRequest(Combat(DimensionCreatureAttackKind.None)), out report);

            Assert.IsNotEmpty(report.Warnings, "It generates fine and does nothing, which is worse.");
        }

        [Test]
        public void ShootingWithNoProjectileIsReported()
        {
            DimensionCreatureCombatTemplate combat = Combat(DimensionCreatureAttackKind.Ranged);
            Assert.IsTrue(combat.RangedIsMissingProjectile);

            DimensionCreatureGenerationReport report;
            Run(CombatRequest(combat), out report);
            Assert.IsNotEmpty(report.Warnings);
        }

        [Test]
        public void SeeingLessFarThanItShootsIsReported()
        {
            // It notices you only once you are already closer than it is willing to fire, so it never
            // shoots — a creature that looks finished and never acts.
            DimensionCreatureCombatTemplate combat = Combat(
                DimensionCreatureAttackKind.Ranged,
                projectile: "SlingshotProjectile",
                detectionRadius: 3f,
                rangedMinDistance: 8f);
            Assert.IsTrue(combat.CannotSeeFarEnoughToShoot);

            DimensionCreatureGenerationReport report;
            Run(CombatRequest(combat), out report);
            Assert.IsNotEmpty(report.Warnings);
        }

        [Test]
        public void ACallerWithNoCombatTemplateGeneratesExactlyAsBefore()
        {
            // The template is optional so existing callers keep working; this pins that promise.
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(Request(Stats(DimensionCreatureStatSource.Authored, 100, 0)), out report);

            Assert.IsNotNull(prefab, string.Join("; ", report.Errors));
            Assert.IsNull(prefab.GetComponent<MeleeAttackStateAuthoring>());
            Assert.IsNull(prefab.GetComponent<NearbyEntitiesTrackerAuthoring>());
        }

        [Test]
        public void AnAuthoredHealthSurvivesOntoThePrefab()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(Request(Stats(DimensionCreatureStatSource.Authored, 450, 0)), out report);

            Assert.IsNotNull(prefab, string.Join("; ", report.Errors));

            HealthAuthoring health = prefab.GetComponent<HealthAuthoring>();
            Assert.IsNotNull(health);
            Assert.AreEqual(450, health.maxHealth);
            Assert.IsTrue(
                health.dontCalculateHealthFromLevel,
                "Left false, Core Keeper recomputes maxHealth from the area level the next time this " +
                "prefab is validated, and the authored number silently disappears.");
        }

        [Test]
        public void AskingForVanillaScalingLeavesTheCurveTurnedOn()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(Request(Stats(DimensionCreatureStatSource.AreaLevelCurve, 450, 0)), out report);

            HealthAuthoring health = prefab.GetComponent<HealthAuthoring>();
            Assert.IsFalse(
                health.dontCalculateHealthFromLevel,
                "An author who asked to scale like vanilla must actually scale like vanilla.");
        }

        [Test]
        public void AnAuthoredDamageReductionIsNotRecomputedFromLevel()
        {
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(Request(Stats(DimensionCreatureStatSource.Authored, 100, 12)), out report);

            DamageReductionAuthoring reduction = prefab.GetComponent<DamageReductionAuthoring>();
            Assert.IsNotNull(reduction);
            Assert.AreEqual(12, reduction.reduction);
            Assert.IsFalse(reduction.calculateReductionFromLevel);
        }

        [Test]
        public void ACreatureWithNoReductionCarriesNoReductionComponent()
        {
            // An empty component is a row the game reads on every hit to learn that nothing happens.
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(Request(Stats(DimensionCreatureStatSource.Authored, 100, 0)), out report);

            Assert.IsNull(prefab.GetComponent<DamageReductionAuthoring>());
        }

        [Test]
        public void ABossIsMarkedAsOneAndAnOrdinaryEnemyIsNot()
        {
            DimensionCreatureGenerationReport report;
            GameObject boss = Run(Request(Stats(DimensionCreatureStatSource.Authored, 100, 0), true), out report);
            Assert.IsNotNull(boss.GetComponent<BossAuthoring>());
            Assert.IsNotNull(boss.GetComponent<EnemyAuthoring>());
        }

        [Test]
        public void AnUnknownBehaviourIsReportedRatherThanQuietlySubstituted()
        {
            // A creature given the wrong AI spawns, moves and fights — just not as intended, which is
            // far harder to notice than one that stands still with a warning next to it.
            DimensionCreatureGenerationReport report;
            GameObject prefab = Run(
                Request(Stats(DimensionCreatureStatSource.Authored, 100, 0), false, "NotARealBehaviour"),
                out report);

            Assert.IsNotEmpty(report.Warnings);
            Assert.IsNull(prefab.GetComponent<BehaviourAuthoring>());
        }

        [Test]
        public void GeneratingTwiceUpdatesTheSamePrefab()
        {
            DimensionCreatureGenerationReport first;
            Run(Request(Stats(DimensionCreatureStatSource.Authored, 100, 0)), out first);

            DimensionCreatureGenerationReport second = DimensionCreatureGenerator.Generate(
                new List<DimensionCreatureGenerator.Request>
                {
                    Request(Stats(DimensionCreatureStatSource.Authored, 250, 0))
                },
                TestRoot,
                default(DimensionNamingContext));

            Assert.AreEqual(1, second.Updated.Count);
            Assert.IsEmpty(second.Created);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(second.Updated[0]);
            Assert.AreEqual(
                250,
                prefab.GetComponent<HealthAuthoring>().maxHealth,
                "Regenerating must carry the new number, not keep the old prefab's.");
        }

        [Test]
        public void ACreatureWithNoStatsIsSkippedRatherThanGeneratedEmpty()
        {
            DimensionCreatureGenerationReport report = DimensionCreatureGenerator.Generate(
                new List<DimensionCreatureGenerator.Request>
                {
                    new DimensionCreatureGenerator.Request { CreatureId = "nostats", Stats = null }
                },
                TestRoot,
                default(DimensionNamingContext));

            Assert.IsEmpty(report.Created);
            Assert.AreEqual(1, report.Skipped.Count);
        }

        // ---- the stat curves, which the dashboard shows before anything is built ----

        [Test]
        public void AuthoredStatsResolveToExactlyWhatWasTyped()
        {
            DimensionCreatureStatsTemplate stats = Stats(DimensionCreatureStatSource.Authored, 450, 0);
            Assert.AreEqual(450, stats.ResolveMaxHealth(5, true, false));
            Assert.AreEqual(
                450,
                stats.ResolveMaxHealth(1, true, true),
                "The area level must not touch an authored number, whatever the creature is.");
        }

        [Test]
        public void ScaledStatsReproduceCoreKeepersOwnCurves()
        {
            DimensionCreatureStatsTemplate stats = Stats(DimensionCreatureStatSource.AreaLevelCurve, 1, 0);

            // The game's own formulas, so the dashboard can show a real number rather than a
            // multiplier: a level-3 boss is 300 * 3^2.45, an enemy is 135 + 15 * (level-1)^2.
            Assert.AreEqual(
                Mathf.RoundToInt(300f * Mathf.Pow(3f, 2.45f)),
                stats.ResolveMaxHealth(3, true, true));
            Assert.AreEqual(
                Mathf.RoundToInt(135f + 15f * Mathf.Pow(2f, 2f)),
                stats.ResolveMaxHealth(3, true, false));
        }

        [Test]
        public void ScaledStatsTreatAMissingLevelAsTheFirstOne()
        {
            DimensionCreatureStatsTemplate stats = Stats(DimensionCreatureStatSource.AreaLevelCurve, 1, 0);
            Assert.AreEqual(stats.ResolveMaxHealth(1, true, false), stats.ResolveMaxHealth(0, true, false));
        }
    }
}
