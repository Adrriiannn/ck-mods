#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using ExpandNullforge.Foundation;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Follows one mod-owned name the whole way: authored, generated, emitted, resolved, written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each hop was individually fine before and the chain still did not work, which is why this is
    /// one test rather than four. The generator kept the component but baked <c>None</c>; nothing
    /// emitted a row; nothing at runtime knew to write the field.
    /// </para>
    /// <para>
    /// The resolver in the last step deliberately knows only the QUALIFIED name, because that is
    /// what the game's own lookup contains. A test whose resolver also knew the bare name would
    /// pass on unqualified strings — with both sides agreeing on the wrong thing — and would miss
    /// exactly the class of bug that made a workbench recipe ship an unqualified output id.
    /// </para>
    /// </remarks>
    internal sealed class DimensionObjectLinkEndToEndTests
    {
        private const string TestFolder = "Assets/ExpandNullforgeObjectLinkTests";

        private readonly List<Object> created = new List<Object>();
        private World world;

        [SetUp]
        public void SetUp()
        {
            DimensionObjectLinkRegistry.Clear();
            DeleteTestFolder();
            world = new World("nullforge-object-link-e2e");
        }

        [TearDown]
        public void TearDown()
        {
            if (world != null && world.IsCreated)
            {
                world.Dispose();
            }

            world = null;
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
            DimensionObjectLinkRegistry.Clear();
            DeleteTestFolder();
        }

        [Test]
        public void AModItemNameSurvivesEveryHopToTheField()
        {
            DimensionProjectileAsset bolt = MakeProjectile("EmberBolt");
            DimensionItemAsset bow = MakeBow("EmberCannon", "EmberBolt");
            DimensionTemplateAsset template = MakeTemplate(bow, bolt);

            // ---- hop 1: the generator keeps the weapon on the prefab ----
            DimensionItemGenerationReport report = DimensionItemGenerator.Generate(
                new[] { bow },
                TestFolder,
                null,
                null,
                null,
                null,
                null,
                null,
                new[] { "EmberBolt" });

            Assert.That(report.Errors, Is.Empty, string.Join("; ", report.Errors));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                TestFolder + "/EmberCannon.prefab");
            Assert.That(prefab, Is.Not.Null, "The bow's prefab was not written.");
            Assert.That(prefab.GetComponent<RangeWeaponAuthoring>(), Is.Not.Null,
                "The weapon component was stripped, so there is nothing left for the runtime to " +
                "write the shot into.");

            foreach (string warning in report.Warnings)
            {
                Assert.That(warning, Does.Not.Contain("EmberBolt"),
                    "One of the mod's own projectiles was reported as a name the game does not " +
                    "have: " + warning);
            }

            // ---- hop 2: the bootstrap emits the row under the name the GENERATOR actually wrote ----
            //
            // The mod name is read back off the prefab rather than typed in here, and that is the
            // whole point of this half. Handing the emitter a literal "MyMod" while the generator
            // resolved its own name from the output folder made the two halves structurally unable
            // to disagree in the test — which is exactly the bug class site 18 was: the generator
            // registered "MyMod:EmberBolt" and the workbench shipped "EmberBolt".
            ObjectAuthoring generatedObject = prefab.GetComponent<ObjectAuthoring>();
            Assert.That(generatedObject, Is.Not.Null, "The bow's prefab carries no object at all.");
            string ownerName = generatedObject.objectName;
            Assert.That(ownerName, Is.Not.Empty, "The generator stamped no object name.");

            string modName = DimensionObjectNamespace.ModOf(ownerName);

            StringBuilder builder = new StringBuilder();
            DimensionRuntimeConsumerBootstrapUtility.AppendObjectLinkRegistrations(
                builder, template, modName);
            string emitted = builder.ToString();

            Assert.That(emitted, Does.Contain("DimensionObjectLinkRegistry.Register("),
                "No row was emitted, so the name never reaches the game.");
            Assert.That(emitted, Does.Contain("\"" + ownerName + "\""),
                "The emitter's owner name is not the name the generator stamped on the prefab. " +
                "The runtime looks the owner up by that exact string, so nothing would be found.");
            Assert.That(
                emitted,
                Does.Contain("\"" + DimensionObjectNamespace.Qualify(modName, "EmberBolt") + "\""));
            Assert.That(emitted, Does.Contain("DimensionObjectLink.FiresProjectile"));

            // ---- hop 3: the row registers and hydrates the prefab entity ----
            MatchCollection literals = Regex.Matches(emitted, "\"([^\"]+)\"");
            Assert.That(literals.Count, Is.GreaterThanOrEqualTo(2));
            DimensionObjectLinkRegistry.Register(
                literals[0].Groups[1].Value,
                DimensionObjectLink.FiresProjectile,
                literals[1].Groups[1].Value);

            DimensionObjectLinkDefinition row = DimensionObjectLinkRegistry.All[0];
            Assert.That(row.OwnerObjectName, Is.EqualTo(ownerName));

            // THE REAL RESOLVER, not a stand-in, on both names. Outside a running game the mod
            // lookup is empty, so both must come back None — and that is worth asserting rather
            // than skipping: a mod id that happens to match an ObjectID member would resolve HERE,
            // to the game's object, and the hydration would then write the game's prefab.
            Assert.That(
                DimensionObjectNames.Resolve(row.OwnerObjectName),
                Is.EqualTo(ObjectID.None),
                "'" + row.OwnerObjectName + "' resolves to one of the game's own objects, so the " +
                "hydration would write the field onto the GAME's prefab, not this mod's.");
            Assert.That(
                DimensionObjectNames.Resolve(row.TargetObjectName),
                Is.EqualTo(ObjectID.None),
                "'" + row.TargetObjectName + "' is one of this mod's names and must not answer to " +
                "an ObjectID member.");

            // And the same resolver DOES answer one of the game's own names with no game running,
            // which is the half the hydration system depends on for a vanilla reference.
            Assert.That(
                DimensionObjectNames.Resolve("WoodArrowProjectile"),
                Is.EqualTo(ObjectID.WoodArrowProjectile));

            // The id the game would hand out for the mod's projectile once it is loaded. There is
            // no way to make the real lookup produce one outside a running game, so this is the
            // one value the test supplies rather than derives.
            const ObjectID whatTheGameWouldHandOut = ObjectID.WoodArrowProjectile;

            Entity entity = world.EntityManager.CreateEntity(typeof(RangeWeaponCD));
            DimensionObjectLinkOutcome outcome = DimensionObjectLinkHydration.Apply(
                world.EntityManager, entity, row, whatTheGameWouldHandOut);

            Assert.That(outcome.Result, Is.EqualTo(DimensionObjectLinkResult.Written));
            Assert.That(
                world.EntityManager.GetComponentData<RangeWeaponCD>(entity).projectileID,
                Is.EqualTo(whatTheGameWouldHandOut));

            // Applied twice is not written twice: the settled ledger and the compare-before-write
            // both depend on this, and a second write would dirty the chunk every tick forever.
            Assert.That(
                DimensionObjectLinkHydration.Apply(
                    world.EntityManager, entity, row, whatTheGameWouldHandOut).Result,
                Is.EqualTo(DimensionObjectLinkResult.AlreadyCorrect));
        }

        [Test]
        public void TheBinderAndTheEmitterAgreeOnEveryDeferredReference()
        {
            // Two independent walks of the same template drifting apart is the one structural risk
            // in this design: the generator defers a reference the emitter never registers, and the
            // field stays None forever with nothing said about it.
            DimensionProjectileAsset bolt = MakeProjectile("EmberBolt");
            DimensionItemAsset bow = MakeBow("EmberCannon", "EmberBolt");
            DimensionItemAsset broken = MakeBow("BrokenCannon", "Embrbolt");
            DimensionTemplateAsset template = MakeTemplate(bow, bolt, broken);

            DimensionObjectBinder binder = new DimensionObjectBinder(
                new DimensionNamingContext(
                    "MyMod", DimensionGeneratedObjectIds.Collect(template)));

            StringBuilder builder = new StringBuilder();
            DimensionRuntimeConsumerBootstrapUtility.AppendObjectLinkRegistrations(
                builder, template, "MyMod");
            string emitted = builder.ToString();

            Assert.That(binder.IsDeferred("EmberBolt"), Is.True);
            Assert.That(emitted, Does.Contain("\"MyMod:EmberBolt\""),
                "The binder deferred this reference and the emitter did not register it.");

            Assert.That(binder.IsDeferred("Embrbolt"), Is.False);
            Assert.That(emitted, Does.Not.Contain("Embrbolt"),
                "The emitter registered a name the binder treats as a typo, so the mistake would " +
                "never be reported and never resolve.");
        }

        [Test]
        public void AWorkbenchRecipeOutputtingAModItemShipsTheQualifiedName()
        {
            // A ForOutputFolder that owns nothing makes QualifyReference the identity function:
            // the station ships "EmberBolt" while the item registered as "MyMod:EmberBolt",
            // the lookup misses, and the station crafts nothing.
            DimensionItemAsset output = MakeBow("EmberBolt", string.Empty);
            DimensionNamingContext owning =
                new DimensionNamingContext("MyMod", new[] { output.ItemId });

            Assert.That(owning.QualifyReference("EmberBolt"), Is.EqualTo("MyMod:EmberBolt"));
            Assert.That(
                new DimensionNamingContext(string.Empty, new[] { "EmberBolt" })
                    .QualifyReference("EmberBolt"),
                Is.EqualTo("EmberBolt"),
                "With no mod name there is nothing to qualify with; the ownership set alone is " +
                "not enough.");
        }

        [Test]
        public void AModDropThatWouldFitAsCustomLootGoesToTheRuntimeRegistryInstead()
        {
            // DropsLootBuffer.lootDropID is a baked ObjectID, so a mod-named drop cannot be written
            // onto the prefab at all. A router that sends it there anyway has the bootstrap
            // skip it as "already written", so the drop lands nowhere.
            DimensionDropSource fixedAmount = MakeDropSource();

            Assert.That(DimensionDropEmitter.FitsInCustomLoot(fixedAmount, "Wood"), Is.True);
            Assert.That(DimensionDropEmitter.FitsInCustomLoot(fixedAmount, "MyMod:Ember"), Is.False);
        }

        [Test]
        public void ASwitchedOffProjectileIsNotTreatedAsANameWaitingToArrive()
        {
            // Untick a projectile and leave the bow pointing at it. Nothing will ever register
            // under that name, so a row for it would leave the game retrying every tick forever
            // with nothing said — which is what it did.
            DimensionProjectileAsset bolt = MakeProjectile("EmberBolt");
            SerializedObject serialized = new SerializedObject(bolt);
            serialized.FindProperty("enabled").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionItemAsset bow = MakeBow("EmberCannon", "EmberBolt");
            DimensionTemplateAsset template = MakeTemplate(bow, bolt);

            Assert.That(
                DimensionGeneratedObjectIds.Collect(template),
                Does.Not.Contain("EmberBolt"),
                "A switched-off asset generates no object, so its id is not one of ours.");
            Assert.That(
                DimensionGeneratedObjectIds.SwitchedOff(template),
                Does.Contain("EmberBolt"),
                "It still has to be recognisable, or the message is the one for a typo.");

            DimensionObjectBinder binder = new DimensionObjectBinder(
                new DimensionNamingContext(
                    "MyMod", DimensionGeneratedObjectIds.Collect(template)),
                DimensionGeneratedObjectIds.SwitchedOff(template));

            Assert.That(binder.IsDeferred("EmberBolt"), Is.False);
            Assert.That(binder.NamesSomethingSwitchedOff("EmberBolt"), Is.True);
            Assert.That(
                binder.ExplainIfSwitchedOff("fires", "EmberBolt"),
                Does.Contain("switched off"));

            LogAssert.Expect(LogType.Warning, new Regex("switched off"));
            StringBuilder builder = new StringBuilder();
            DimensionRuntimeConsumerBootstrapUtility.AppendObjectLinkRegistrations(
                builder, template, "MyMod");

            Assert.That(
                builder.ToString(),
                Does.Not.Contain("EmberBolt"),
                "A row was written for a name nothing will ever answer to.");
        }

        [Test]
        public void AModCreatureShootingAModProjectileIsRegistered()
        {
            // The single most obvious mod-to-mod reference there is, and the one the framework
            // could not do: a creature's shot is RangeAttackStateCD.projectileID, a different
            // component from a bow's, so the weapon member could never have covered it.
            DimensionProjectileAsset bolt = MakeProjectile("EmberBolt");
            DimensionMobAsset wisp = MakeRangedMob("EmberWisp", "EmberBolt");
            DimensionTemplateAsset template = MakeCreatureTemplate(bolt, wisp);

            StringBuilder builder = new StringBuilder();
            DimensionRuntimeConsumerBootstrapUtility.AppendObjectLinkRegistrations(
                builder, template, "MyMod");
            string emitted = builder.ToString();

            Assert.That(emitted, Does.Contain("DimensionObjectLink.CreatureShot"));
            Assert.That(emitted, Does.Contain("\"MyMod:EmberWisp\""));
            Assert.That(emitted, Does.Contain("\"MyMod:EmberBolt\""));

            Entity entity = world.EntityManager.CreateEntity(typeof(RangeAttackStateCD));
            DimensionObjectLinkOutcome outcome = DimensionObjectLinkHydration.Apply(
                world.EntityManager,
                entity,
                new DimensionObjectLinkDefinition(
                    "MyMod:EmberWisp", DimensionObjectLink.CreatureShot, "MyMod:EmberBolt"),
                ObjectID.WoodArrowProjectile);

            Assert.That(outcome.Result, Is.EqualTo(DimensionObjectLinkResult.Written));
            Assert.That(
                world.EntityManager.GetComponentData<RangeAttackStateCD>(entity).projectileID,
                Is.EqualTo(ObjectID.WoodArrowProjectile));
        }

        [Test]
        public void AnExplosiveWindupShotKeepsItsSizeWithoutMakingTheGameLogAnError()
        {
            // RangeWeaponConverter logs a red error naming the creator's prefab whenever an
            // explosion size is set and the explosive shot is None — the exact state a deferred
            // wound-up shot has to bake as. The size is held back and travels on the row instead.
            DimensionProjectileAsset bolt = MakeProjectile("EmberBolt");
            DimensionItemAsset cannon = MakeBow("EmberCannon", "EmberBolt");
            SerializedObject serialized = new SerializedObject(cannon);
            SerializedProperty weapon = serialized.FindProperty("weapon");
            weapon.FindPropertyRelative("secondProjectileId").stringValue = "EmberBolt";
            weapon.FindPropertyRelative("explosionSize").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionTemplateAsset template = MakeTemplate(cannon, bolt);

            StringBuilder builder = new StringBuilder();
            DimensionRuntimeConsumerBootstrapUtility.AppendObjectLinkRegistrations(
                builder, template, "MyMod");

            Assert.That(builder.ToString(), Does.Contain("DimensionObjectLink.SecondProjectile"));

            Entity entity = world.EntityManager.CreateEntity(typeof(RangeWeaponCD));
            DimensionObjectLinkHydration.Apply(
                world.EntityManager,
                entity,
                new DimensionObjectLinkDefinition(
                    "MyMod:EmberCannon",
                    DimensionObjectLink.SecondProjectile,
                    "MyMod:EmberBolt",
                    -1,
                    0,
                    0,
                    -1,
                    3),
                ObjectID.WoodArrowProjectile);

            RangeWeaponCD written = world.EntityManager.GetComponentData<RangeWeaponCD>(entity);
            Assert.That(written.windupProjectileID, Is.EqualTo(ObjectID.WoodArrowProjectile));
            Assert.That(written.explosionSize, Is.EqualTo(3),
                "The size the creator typed never came back, so the shot exploded for nothing.");
        }

        [Test]
        public void AModCreatureDroppingAModItemShipsTheTableItWasGivenToHangDropsOn()
        {
            // THE CAPABILITY THE WAVE EXISTS FOR, driven rather than grepped for.
            //
            // The generator stamps a minted loot table onto a creature that has none, so an item of
            // the mod's own has somewhere to drop from. Reading that
            // table back off the creature's prefab ENTITY at load, through PugDatabase, happens
            // inside a Harmony
            // prefix on LootTableConverter.Convert, which runs during database conversion, when
            // Manager.ecs has no world to answer with. The read fails every time, the drop is
            // reported as belonging to a creature with no loot table, and the pass latches.
            //
            // So the row carries the table's name. This asserts it is on the row, and that the id
            // the runtime mints from that name is the id the editor stamps — the two halves of the
            // identity that must never drift.
            DimensionMobAsset grubby = MakeRangedMob("Grubby", string.Empty);
            DimensionItemAsset shard = MakeDroppedItem("EmberShard", "Grubby", 2, 5);
            DimensionTemplateAsset template = MakeDropTemplate(grubby, shard);

            StringBuilder builder = new StringBuilder();
            DimensionRuntimeConsumerBootstrapUtility.AppendAuthoredDropRegistrations(
                builder, template, "MyMod");
            string emitted = builder.ToString();

            Assert.That(
                emitted,
                Does.Contain("DimensionPortalDropRegistry.Register("),
                "An amount range cannot be carried as custom loot, so this drop has to be " +
                "registered at load or it happens nowhere.");
            Assert.That(emitted, Does.Contain("\"MyMod:Grubby\""));
            Assert.That(emitted, Does.Contain("\"MyMod:EmberShard\""));

            string expectedTable = ExpandNullforge.Loot.DimensionLootTableRegistry
                .AutoTableNameFor("MyMod:Grubby");
            Assert.That(
                emitted,
                Does.Contain("\"" + expectedTable + "\""),
                "The row does not say which loot table the drop goes in, so the injection has to " +
                "read it back off a prefab entity — which is exactly what it cannot do while the " +
                "database is being converted.");

            Assert.That(
                DimensionDropEmitter.LootTableIdFor(expectedTable),
                Is.EqualTo((LootTableID)ExpandNullforge.Loot.DimensionLootTableRegistry
                    .ComputeLootTableId(expectedTable)),
                "The editor stamps one id onto the prefab and the runtime mints another from the " +
                "same name, so the drop lands in a table the creature does not carry.");
            Assert.That(
                (int)DimensionDropEmitter.LootTableIdFor(expectedTable),
                Is.GreaterThanOrEqualTo(
                    ExpandNullforge.Loot.DimensionLootTableRegistry.MinCustomLootTableId),
                "An auto table must never mint an id in the game's own range.");

            // THE GIVING, DRIVEN. The assertions above are the emitter's half. Without this the
            // whole test survives the stamp being taken out of ApplyLoot, which is the one write
            // that puts the table on the creature at all.
            GameObject creature = TrackObject(new GameObject("Grubby"));
            DimensionDropEmitter.EnsureALootTableToHangDropsOn(creature, "MyMod:Grubby");

            DropLootAuthoring stamped = creature.GetComponent<DropLootAuthoring>();
            Assert.That(
                stamped,
                Is.Not.Null,
                "A creature with no loot table of its own was left without one, so there is " +
                "nowhere for the drop to be registered and it happens nowhere.");
            Assert.That(stamped.hasLootTable, Is.True);
            Assert.That(
                stamped.lootTableID,
                Is.EqualTo(DimensionDropEmitter.LootTableIdFor(expectedTable)),
                "The id stamped on the prefab is not the id the row's table name mints, so the " +
                "drop lands in a table the creature does not carry.");
        }

        [Test]
        public void AnAuthoredGameLootTableOnAModCreatureIsSaidRatherThanUsedQuietly()
        {
            // Give a creature one of the game's own tables and a drop authored against it is
            // appended to that shared table, so everything else in the game that draws from it
            // starts dropping the mod's item too. That is a legitimate thing to author and an
            // appalling thing to do by accident, so both halves agree on the name and the
            // generator says what it means.
            Assert.That(
                DimensionDropEmitter.LootTableIdFor("SlimeBoss"),
                Is.EqualTo(LootTableID.SlimeBoss),
                "A table the game already has must never be minted a new id, or the drop lands in " +
                "a table nothing carries.");

            // THE SAYING, DRIVEN. Asserting two pure functions and nothing else leaves this green
            // after the whole reporting block is deleted, under a name that promises the
            // opposite.
            GameObject creature = TrackObject(new GameObject("Slimey"));
            DropLootAuthoring authored = creature.AddComponent<DropLootAuthoring>();
            authored.hasLootTable = true;
            authored.lootTableID = LootTableID.SlimeBoss;

            List<string> said = new List<string>();
            DimensionDropEmitter.EnsureALootTableToHangDropsOn(
                creature, "MyMod:Slimey", delegate(string message) { said.Add(message); });

            Assert.That(
                said.Count,
                Is.EqualTo(1),
                "Hanging a mod's drop on one of the game's shared tables was not said, so every " +
                "other thing in the game that draws from that table starts dropping it silently.");
            Assert.That(said[0], Does.Contain("SlimeBoss"));
            Assert.That(
                authored.lootTableID,
                Is.EqualTo(LootTableID.SlimeBoss),
                "An authored table must be left alone, not replaced by a minted one.");
        }

        [Test]
        public void ClearingEveryDropAndGeneratingAgainTakesBackWhatTheLastGenerateWrote()
        {
            // The later-pass clobber class, from the other side. A chest or a world object reloads
            // the prefab it wrote last time, so anything the drop path only ever switched ON stayed
            // on for ever: delete every drop, generate again, and it kept dropping them.
            GameObject chest = TrackObject(new GameObject("Chest"));
            DropLootAuthoring loot = chest.AddComponent<DropLootAuthoring>();
            loot.hasLootTable = true;
            loot.lootTableID = (LootTableID)ExpandNullforge.Loot.DimensionLootTableRegistry
                .ComputeLootTableId("OldMod:Chest drops");
            loot.hasCustomLoot = true;
            loot.customLoot = new CustomLoot { chance = 1f, Values = new List<LootDrop>() };
            loot.hasLootDropsOnTakingDamage = true;

            DimensionDropEmitter.ClearAnyLootTheLastGenerateWrote(chest);

            Assert.That(loot.hasLootTable, Is.False);
            Assert.That(loot.lootTableID, Is.EqualTo(LootTableID.Empty));
            Assert.That(loot.hasCustomLoot, Is.False);
            Assert.That(
                loot.hasLootDropsOnTakingDamage,
                Is.True,
                "Shedding belongs to a different pass, so taking back the drops must not touch it.");
        }

        [Test]
        public void EveryGeneratorThatHangsDropsOnATableAlsoTakesBackTheLastOne()
        {
            // The claim written into EnsureALootTableToHangDropsOn is that hasLootTable is only ever
            // true because THIS generate set it. That claim was recorded as an invariant while one
            // generator in three did the clearing, so it is checked rather than believed.
            string[] generators =
            {
                "Editor/Generators/Creatures/DimensionCreatureGenerator.cs",
                "Editor/Generators/Objects/DimensionContainerGenerator.cs",
                "Editor/Generators/World/DimensionWorldObjectGenerator.cs"
            };

            foreach (string generator in generators)
            {
                // Found by name rather than by path: this list names three generators, not three
                // places, and where they live is not what it is asserting.
                string source = DimensionFrameworkSourceScanner.ReadPartials(
                    System.IO.Path.GetFileNameWithoutExtension(generator));

                Assert.That(
                    StripComments(source),
                    Does.Contain("ClearAnyLootTheLastGenerateWrote("),
                    generator + " hangs drops on a loot table without taking back the one the " +
                    "last generate stamped, so a deleted drop keeps dropping and a renamed mod " +
                    "leaves the object pointing at a table nothing fills.");
            }
        }

        [Test]
        public void AnAuthoredDropChanceBecomesTheChanceTheGameActuallyRolls()
        {
            // THE WORST KIND OF DEFECT THIS PROJECT HAS: the control said "how likely it is to drop
            // at all" and the game handed the item out on every kill. A loot table holds no chance
            // per row — it holds a count of picks and a weight each — so the chance has to be turned
            // into both, with a row of nothing taking up the rest of the whole. That is how 74 of
            // the game's own 176 tables express a rare drop.
            LootTable table = new LootTable
            {
                id = (LootTableID)1234,
                minUniqueDrops = 1,
                maxUniqueDrops = 1,
                dontAllowDuplicates = true,
                lootInfos = new List<LootInfo>
                {
                    new LootInfo
                    {
                        objectID = ObjectID.IronOre,
                        weight = 1f,
                        editorVisualDropChance = 5f,
                        amount = new Pug.UnityExtensions.RangeInt { min = 2, max = 5 }
                    }
                },
                guaranteedLootInfos = new List<LootInfo>()
            };

            ExpandNullforge.Portals.DimensionPortalDropRegistry.ShapeAMintedTable(table);

            Assert.That(
                table.minUniqueDrops,
                Is.EqualTo(1),
                "A table whose chances fit in one pick must make exactly one, or every row's " +
                "chance moves with the count.");
            Assert.That(table.maxUniqueDrops, Is.EqualTo(1));
            Assert.That(
                table.dontAllowDuplicates,
                Is.False,
                "Barring duplicates reads a non-stackable item's amount range as a number of " +
                "copies and stops after the first, so '2 to 5 of my sword' would ship one.");

            LootInfo authored = table.lootInfos.Find(row => row.objectID == ObjectID.IronOre);
            Assert.That(authored, Is.Not.Null);
            Assert.That(
                authored.weight,
                Is.EqualTo(0.05f).Within(0.0001f),
                "The number the creator typed is not the share the game picks on, so it is being " +
                "read as a probability and behaving as a weight.");

            LootInfo nothing = table.lootInfos.Find(row => row.objectID == ObjectID.None);
            Assert.That(
                nothing,
                Is.Not.Null,
                "Without a row of nothing the only row wins every pick, whatever its weight, and " +
                "a one-in-twenty drop happens every time.");
            Assert.That(nothing.weight, Is.EqualTo(0.95f).Within(0.0001f));
        }

        [Test]
        public void AnAlwaysDropLeavesTheChanceDropsAPickOfTheirOwn()
        {
            // An always-drop is filled first and eats one of the table's picks, so a table that
            // handed out one thing gave the guaranteed row the only slot and no chance row could
            // ever come out.
            LootTable table = new LootTable
            {
                id = (LootTableID)1235,
                minUniqueDrops = 1,
                maxUniqueDrops = 1,
                dontAllowDuplicates = false,
                lootInfos = new List<LootInfo>
                {
                    new LootInfo
                    {
                        objectID = ObjectID.IronOre,
                        editorVisualDropChance = 20f,
                        amount = new Pug.UnityExtensions.RangeInt { min = 1, max = 1 }
                    }
                },
                guaranteedLootInfos = new List<LootInfo>
                {
                    new LootInfo
                    {
                        objectID = ObjectID.Wood,
                        weight = 1f,
                        isPartOfGuaranteedDrop = true,
                        amount = new Pug.UnityExtensions.RangeInt { min = 1, max = 1 }
                    }
                }
            };

            ExpandNullforge.Portals.DimensionPortalDropRegistry.ShapeAMintedTable(table);

            Assert.That(
                table.maxUniqueDrops,
                Is.EqualTo(2),
                "The always-drop takes a pick of its own, so a table left at one had nothing left " +
                "for the chance drops and they could never happen.");
            Assert.That(table.minUniqueDrops, Is.EqualTo(2));
        }

        [Test]
        public void ChancesThatDoNotFitInOnePickAreGivenMorePicksRatherThanBeingSquashed()
        {
            List<float> two80s = new List<float> { 0.8f, 0.8f };
            int rolls = ExpandNullforge.Portals.DimensionPortalDropRegistry
                .RollsNeededForChances(two80s);

            Assert.That(
                rolls,
                Is.GreaterThan(1),
                "Two things at 80% cannot both come out of one pick, so squeezing them into one " +
                "would halve both of the numbers the creator typed.");

            float share = ExpandNullforge.Portals.DimensionPortalDropRegistry
                .ShareForChance(0.8f, rolls);
            float actual = 1f - Mathf.Pow(1f - share, rolls);
            Assert.That(
                actual,
                Is.EqualTo(0.8f).Within(0.001f),
                "The share worked out for this many picks does not produce the authored chance.");
            Assert.That(
                share * two80s.Count,
                Is.LessThanOrEqualTo(1f),
                "The shares do not fit inside one whole, so the game would renormalise them and " +
                "every chance would come out higher than it was written.");
        }

        // ---- fixtures ------------------------------------------------------------------

        private DimensionItemAsset MakeDroppedItem(
            string itemId,
            string sourceId,
            int minAmount,
            int maxAmount)
        {
            DimensionItemAsset item = Track(ScriptableObject.CreateInstance<DimensionItemAsset>());
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = itemId;
            serialized.FindProperty("enabled").boolValue = true;

            SerializedProperty sources = serialized.FindProperty("dropsFrom");
            sources.arraySize = 1;
            SerializedProperty source = sources.GetArrayElementAtIndex(0);
            source.FindPropertyRelative("enabled").boolValue = true;
            source.FindPropertyRelative("kind").enumValueIndex =
                (int)DimensionDropSourceKind.Creature;
            source.FindPropertyRelative("sourceId").stringValue = sourceId;
            source.FindPropertyRelative("chance").floatValue = 1f;
            source.FindPropertyRelative("weight").intValue = 1;

            // A RANGE is the point: custom loot carries one amount, so this is a drop that can only
            // live in a loot table — the shape a creature authored without one had nowhere to put.
            source.FindPropertyRelative("minAmount").intValue = minAmount;
            source.FindPropertyRelative("maxAmount").intValue = maxAmount;
            source.FindPropertyRelative("onlyInBiomeId").stringValue = string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private DimensionTemplateAsset MakeDropTemplate(
            DimensionMobAsset mob,
            DimensionItemAsset item)
        {
            DimensionTemplateAsset template =
                Track(ScriptableObject.CreateInstance<DimensionTemplateAsset>());
            SerializedObject serialized = new SerializedObject(template);
            SerializedProperty mobs = serialized.FindProperty("globalMobs");
            mobs.arraySize = 1;
            mobs.GetArrayElementAtIndex(0).objectReferenceValue = mob;
            SerializedProperty items = serialized.FindProperty("globalItems");
            items.arraySize = 1;
            items.GetArrayElementAtIndex(0).objectReferenceValue = item;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return template;
        }

        private DimensionMobAsset MakeRangedMob(string mobId, string shootsProjectileId)
        {
            DimensionMobAsset mob = Track(ScriptableObject.CreateInstance<DimensionMobAsset>());
            SerializedObject serialized = new SerializedObject(mob);
            serialized.FindProperty("mobId").stringValue = mobId;
            serialized.FindProperty("enabled").boolValue = true;
            SerializedProperty combat = serialized.FindProperty("combat");
            combat.FindPropertyRelative("attackKind").enumValueIndex =
                (int)DimensionCreatureAttackKind.Ranged;
            combat.FindPropertyRelative("projectileItemId").stringValue = shootsProjectileId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return mob;
        }

        private DimensionTemplateAsset MakeCreatureTemplate(
            DimensionProjectileAsset projectile,
            DimensionMobAsset mob)
        {
            DimensionTemplateAsset template =
                Track(ScriptableObject.CreateInstance<DimensionTemplateAsset>());
            SerializedObject serialized = new SerializedObject(template);
            SerializedProperty projectiles = serialized.FindProperty("globalProjectiles");
            projectiles.arraySize = 1;
            projectiles.GetArrayElementAtIndex(0).objectReferenceValue = projectile;
            SerializedProperty mobs = serialized.FindProperty("globalMobs");
            mobs.arraySize = 1;
            mobs.GetArrayElementAtIndex(0).objectReferenceValue = mob;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return template;
        }


        private DimensionDropSource MakeDropSource()
        {
            // Built through the same serialization the assets use, so the defaults are the real ones.
            DimensionItemAsset carrier = Track(ScriptableObject.CreateInstance<DimensionItemAsset>());
            SerializedObject serialized = new SerializedObject(carrier);
            SerializedProperty sources = serialized.FindProperty("dropsFrom");
            sources.arraySize = 1;
            SerializedProperty source = sources.GetArrayElementAtIndex(0);
            source.FindPropertyRelative("sourceId").stringValue = "Slime";
            source.FindPropertyRelative("minAmount").intValue = 1;
            source.FindPropertyRelative("maxAmount").intValue = 1;
            source.FindPropertyRelative("onlyInBiomeId").stringValue = string.Empty;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return carrier.DropsFrom[0];
        }

        private DimensionItemAsset MakeBow(string itemId, string firesProjectileId)
        {
            DimensionItemAsset item = Track(ScriptableObject.CreateInstance<DimensionItemAsset>());
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = itemId;
            serialized.FindProperty("enabled").boolValue = true;
            serialized.FindProperty("archetype").enumValueIndex =
                (int)DimensionItemArchetype.Weapon;
            if (!string.IsNullOrEmpty(firesProjectileId))
            {
                SerializedProperty weapon = serialized.FindProperty("weapon");
                weapon.FindPropertyRelative("kind").enumValueIndex = (int)DimensionWeaponKind.Ranged;
                weapon.FindPropertyRelative("firesProjectileId").stringValue = firesProjectileId;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private DimensionProjectileAsset MakeProjectile(string projectileId)
        {
            DimensionProjectileAsset projectile =
                Track(ScriptableObject.CreateInstance<DimensionProjectileAsset>());
            SerializedObject serialized = new SerializedObject(projectile);
            serialized.FindProperty("projectileId").stringValue = projectileId;
            serialized.FindProperty("enabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return projectile;
        }

        private DimensionTemplateAsset MakeTemplate(
            DimensionItemAsset item,
            DimensionProjectileAsset projectile,
            DimensionItemAsset second = null)
        {
            DimensionTemplateAsset template =
                Track(ScriptableObject.CreateInstance<DimensionTemplateAsset>());
            SerializedObject serialized = new SerializedObject(template);
            SerializedProperty items = serialized.FindProperty("globalItems");
            items.arraySize = second == null ? 1 : 2;
            items.GetArrayElementAtIndex(0).objectReferenceValue = item;
            if (second != null)
            {
                items.GetArrayElementAtIndex(1).objectReferenceValue = second;
            }

            SerializedProperty projectiles = serialized.FindProperty("globalProjectiles");
            projectiles.arraySize = 1;
            projectiles.GetArrayElementAtIndex(0).objectReferenceValue = projectile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return template;
        }

        private T Track<T>(T asset) where T : Object
        {
            created.Add(asset);
            return asset;
        }

        /// <summary>A scene object the teardown will destroy.</summary>
        private GameObject TrackObject(GameObject go)
        {
            created.Add(go);
            return go;
        }

        /// <summary>
        /// A source file with its comments taken out, so a grep for a call cannot be satisfied by
        /// prose about that call.
        /// </summary>
        private static string StripComments(string source)
        {
            string withoutBlocks = Regex.Replace(
                source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            return Regex.Replace(withoutBlocks, @"//[^\r\n]*", string.Empty);
        }

        private static void DeleteTestFolder()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.DeleteAsset(TestFolder);
            }
        }
    }
}
#endif
