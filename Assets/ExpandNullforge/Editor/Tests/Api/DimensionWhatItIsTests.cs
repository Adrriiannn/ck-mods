#if UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using ExpandNullforge.Api;
using ExpandNullforge.Authoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTools
{
    /// <summary>
    /// Proves the one answer the game reads off an item actually reaches the prefab: every kind
    /// translates to the game's own number, a sword generates as a melee weapon rather than as
    /// nothing in particular, durability comes out at the number the game's own formula gives, and
    /// an item written before this question existed still generates the object it always did.
    ///
    /// Shaped after DimensionItemGeneratorTests: prefabs are written for real against the
    /// AssetDatabase, so these run in Unity rather than in the offline compile check.
    /// </summary>
    internal sealed class DimensionWhatItIsTests
    {
        private const string TestFolder = "Assets/ExpandNullforgeWhatItIsTests";

        private readonly List<DimensionItemAsset> created = new List<DimensionItemAsset>();

        [SetUp]
        public void SetUp()
        {
            DeleteTestFolder();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
            DeleteTestFolder();
        }

        // ------------------------------------------------------- the translation ---

        /// <summary>
        /// The whole map, written out. A table read against a table is the only way to catch a case
        /// silently landing on the wrong number, which is invisible in game until somebody notices
        /// their pickaxe digs like a shovel.
        /// </summary>
        [Test]
        public void EveryKind_TranslatesToTheGamesOwnNumber()
        {
            AssertKind(DimensionWhatItIs.Helmet, ObjectType.Helm);
            AssertKind(DimensionWhatItIs.ChestArmour, ObjectType.BreastArmor);
            AssertKind(DimensionWhatItIs.PantsArmour, ObjectType.PantsArmor);
            AssertKind(DimensionWhatItIs.Necklace, ObjectType.Necklace);
            AssertKind(DimensionWhatItIs.Ring, ObjectType.Ring);
            AssertKind(DimensionWhatItIs.OffHand, ObjectType.Offhand);
            AssertKind(DimensionWhatItIs.Bag, ObjectType.Bag);
            AssertKind(DimensionWhatItIs.Lantern, ObjectType.Lantern);
            AssertKind(DimensionWhatItIs.Pouch, ObjectType.Pouch);
            AssertKind(DimensionWhatItIs.Pet, ObjectType.Pet);

            AssertKind(DimensionWhatItIs.MeleeWeapon, ObjectType.MeleeWeapon);
            AssertKind(DimensionWhatItIs.RangedWeapon, ObjectType.RangeWeapon);
            AssertKind(DimensionWhatItIs.ThrowingWeapon, ObjectType.ThrowingWeapon);
            AssertKind(DimensionWhatItIs.SummoningWeapon, ObjectType.SummoningWeapon);
            AssertKind(DimensionWhatItIs.BeamWeapon, ObjectType.BeamWeapon);

            AssertKind(DimensionWhatItIs.Pickaxe, ObjectType.MiningPick);
            AssertKind(DimensionWhatItIs.Shovel, ObjectType.Shovel);
            AssertKind(DimensionWhatItIs.Hoe, ObjectType.Hoe);
            AssertKind(DimensionWhatItIs.Sledgehammer, ObjectType.Sledge);
            AssertKind(DimensionWhatItIs.Drill, ObjectType.DrillTool);
            AssertKind(DimensionWhatItIs.RoofingGadget, ObjectType.RoofingTool);
            AssertKind(DimensionWhatItIs.Paintbrush, ObjectType.PaintTool);
            AssertKind(DimensionWhatItIs.FishingRod, ObjectType.FishingRod);
            AssertKind(DimensionWhatItIs.BugNet, ObjectType.BugNet);
            AssertKind(DimensionWhatItIs.WateringCan, ObjectType.WaterCan);
            AssertKind(DimensionWhatItIs.Bucket, ObjectType.Bucket);
            AssertKind(DimensionWhatItIs.Seeder, ObjectType.Seeder);
            AssertKind(DimensionWhatItIs.CastItem, ObjectType.CastingItem);

            AssertKind(DimensionWhatItIs.SomethingYouPlace, ObjectType.PlaceablePrefab);
            AssertKind(DimensionWhatItIs.Food, ObjectType.Eatable);
            AssertKind(DimensionWhatItIs.Instrument, ObjectType.Instrument);
            AssertKind(DimensionWhatItIs.Valuable, ObjectType.Valuable);
            AssertKind(DimensionWhatItIs.KeyItem, ObjectType.KeyItem);
            AssertKind(
                DimensionWhatItIs.UniqueCraftingComponent, ObjectType.UniqueCraftingComponent);
            AssertKind(DimensionWhatItIs.Critter, ObjectType.Critter);
            AssertKind(DimensionWhatItIs.NothingInParticular, ObjectType.NonUsable);
            AssertKind(DimensionWhatItIs.NotSaid, ObjectType.NonUsable);
        }

        /// <summary>
        /// A kind added to the list and forgotten in the translation would fall through to NonUsable
        /// and be exactly as broken as the bug this whole thing fixes, with nothing to show for it.
        /// </summary>
        [Test]
        public void NoKind_FallsThroughToNothingInParticularByAccident()
        {
            foreach (DimensionWhatItIs kind in Enum.GetValues(typeof(DimensionWhatItIs)))
            {
                if (kind == DimensionWhatItIs.NotSaid ||
                    kind == DimensionWhatItIs.NothingInParticular)
                {
                    continue;
                }

                Assert.That(
                    DimensionItemObjectTypes.ToObjectType(kind),
                    Is.Not.EqualTo(ObjectType.NonUsable),
                    kind + " has no case in DimensionItemObjectTypes, so it generates as an item " +
                    "the player can hold and never use.");
            }
        }

        /// <summary>Every kind is named, so no report line ever prints a bare enum member.</summary>
        [Test]
        public void EveryKind_HasAPlainName()
        {
            foreach (DimensionWhatItIs kind in Enum.GetValues(typeof(DimensionWhatItIs)))
            {
                string described = DimensionWhatItIsRules.Describe(kind);
                Assert.That(described, Is.Not.Empty);
                Assert.That(
                    described,
                    Is.Not.EqualTo(kind.ToString()),
                    kind + " has no plain name, so a report line would read as the enum member.");
            }
        }

        // ------------------------------------------------------------ the object ---

        [Test]
        public void ASword_GeneratesAsAMeleeWeaponRatherThanAsNothingInParticular()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Weapon,
                "mod_sword",
                DimensionWhatItIs.MeleeWeapon,
                serialized =>
                {
                    serialized.FindProperty("damageAmount").intValue = 17;
                    serialized.FindProperty("cooldownSeconds").floatValue = 0.4f;
                });

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);
            Assert.That(report.Errors, Is.Empty, string.Join("; ", report.Errors));

            ObjectAuthoring generated = LoadObject("mod_sword");
            Assert.That(
                generated.objectType,
                Is.EqualTo(ObjectType.MeleeWeapon),
                "A sword typed NonUsable equips to the non-usable slot and cannot be swung.");
        }

        [Test]
        public void AHelmet_GeneratesAsAHelmAndTheGamesOwnNinety()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Armor,
                "mod_helm",
                DimensionWhatItIs.Helmet);

            DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            GameObject prefab = LoadPrefab("mod_helm");
            Assert.That(
                prefab.GetComponent<ObjectAuthoring>().objectType, Is.EqualTo(ObjectType.Helm));

            DurabilityAuthoring durability = prefab.GetComponent<DurabilityAuthoring>();
            Assert.That(durability, Is.Not.Null);
            Assert.That(
                durability.maxDurability,
                Is.EqualTo(90),
                "The game's own base durability for a helm is 90; a generated one used to be 1.");
            Assert.That(
                prefab.GetComponent<ObjectAuthoring>().initialAmount,
                Is.EqualTo(90),
                "On anything that wears out, initialAmount IS the durability the item starts with.");
        }

        [Test]
        public void ADurabilityMultiplier_ScalesTheGamesOwnBase()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Armor,
                "mod_stout_helm",
                DimensionWhatItIs.Helmet,
                serialized => serialized.FindProperty("durabilityMultiplier").floatValue = 2f);

            DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(
                LoadPrefab("mod_stout_helm").GetComponent<DurabilityAuthoring>().maxDurability,
                Is.EqualTo(180),
                "Durability is the type's base times the multiplier, which is the dial that works.");
        }

        [Test]
        public void AMeleeWeapon_GetsThreeHundredAndFiftyRatherThanOne()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Weapon,
                "mod_blade_of_use",
                DimensionWhatItIs.MeleeWeapon,
                serialized =>
                {
                    serialized.FindProperty("damageAmount").intValue = 12;

                    // The game's own melee base is 350 x multiplier x 0.4 / cooldown, so the
                    // ordinary swing time gives the ordinary number back.
                    serialized.FindProperty("cooldownSeconds").floatValue = 0.4f;
                });

            DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            GameObject prefab = LoadPrefab("mod_blade_of_use");
            Assert.That(prefab.GetComponent<DurabilityAuthoring>().maxDurability, Is.EqualTo(350));
            Assert.That(prefab.GetComponent<DurabilityAuthoring>().durability, Is.EqualTo(350));
            Assert.That(prefab.GetComponent<ObjectAuthoring>().initialAmount, Is.EqualTo(350));
        }

        /// <summary>
        /// A slower weapon wears out SOONER, because the game divides the base by the swing time:
        /// 350 x 0.4 / 0.8 is 175. Reading it the other way round makes this
        /// a green test teaching a modder to lengthen a swing for durability, which is worse than
        /// no test.
        /// This is the half of the formula a flat number could never have reproduced.
        /// </summary>
        [Test]
        public void ASlowerMeleeWeapon_WearsOutSooner()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Weapon,
                "mod_heavy_blade",
                DimensionWhatItIs.MeleeWeapon,
                serialized =>
                {
                    serialized.FindProperty("damageAmount").intValue = 40;
                    serialized.FindProperty("cooldownSeconds").floatValue = 0.8f;
                });

            DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(
                LoadPrefab("mod_heavy_blade").GetComponent<DurabilityAuthoring>().maxDurability,
                Is.EqualTo(175),
                "350 x 0.4 / 0.8 is 175.");
        }

        /// <summary>
        /// A weapon with no cooldown would have had its durability divided by nothing. The game's
        /// own default goes in at the cooldown field instead, and the author is told it did.
        /// </summary>
        /// <remarks>
        /// The number is written where cooldowns are written, not as a side effect of the durability
        /// sum, and the component is left switched on: a disabled cooldown would still bake a
        /// CooldownCD of zero, which every slot honours as "no delay at all". It also has to still
        /// be on the prefab at the end — the effects pass runs after the cooldown is written, and
        /// a second cooldown field inside the effects block would delete the component whenever
        /// that field is blank, which is nearly always.
        /// </remarks>
        [Test]
        public void AMeleeWeaponWithNoCooldown_GetsTheGamesOwnDefaultRatherThanADivisionByZero()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Weapon,
                "mod_untimed_blade",
                DimensionWhatItIs.MeleeWeapon,
                serialized => serialized.FindProperty("damageAmount").intValue = 5);

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            GameObject prefab = LoadPrefab("mod_untimed_blade");
            CooldownAuthoring cooldown = prefab.GetComponent<CooldownAuthoring>();
            Assert.That(
                cooldown,
                Is.Not.Null,
                "The effects pass runs last and used to delete this component outright.");
            Assert.That(
                cooldown.cooldown,
                Is.EqualTo(DimensionItemObjectTypes.MeleeFallbackCooldown).Within(0.0001f));
            Assert.That(
                cooldown.enabled,
                Is.True,
                "A switched-off cooldown component still bakes a cooldown of zero.");
            Assert.That(prefab.GetComponent<DurabilityAuthoring>().maxDurability, Is.EqualTo(350));
            Assert.That(report.Derived, Has.Some.Contains("no time between uses was set"));
        }

        /// <summary>
        /// The number a creator types is the number on the prefab.
        /// </summary>
        /// <remarks>
        /// ONE COOLDOWN FIELD, NOT TWO. With a second one inside the effects block, the effects
        /// pass runs after everything else and the field nobody
        /// can see wins: with its own field blank it destroys the cooldown component and takes the
        /// typed number with it, and a melee weapon comes out at the game's default 350 whatever is
        /// typed. This is the test that the one field lands: 0.2 seconds is twice
        /// the swing rate of the 0.4 default, so 350 x 0.4 / 0.2 is 700.
        /// </remarks>
        [Test]
        public void ATypedTimeBetweenUses_ReachesTheGeneratedWeapon()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Weapon,
                "mod_quick_blade",
                DimensionWhatItIs.MeleeWeapon,
                serialized =>
                {
                    serialized.FindProperty("damageAmount").intValue = 7;
                    serialized.FindProperty("cooldownSeconds").floatValue = 0.2f;
                });

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            GameObject prefab = LoadPrefab("mod_quick_blade");
            CooldownAuthoring cooldown = prefab.GetComponent<CooldownAuthoring>();
            Assert.That(
                cooldown,
                Is.Not.Null,
                "The typed cooldown has to survive the effects pass.");
            Assert.That(cooldown.cooldown, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(
                prefab.GetComponent<DurabilityAuthoring>().maxDurability,
                Is.EqualTo(700),
                "350 x 0.4 / 0.2 is 700, which only happens if the typed number reached the game.");
            Assert.That(
                report.Derived,
                Has.None.Contains("no time between uses was set"),
                "A number was typed, so nothing was worked out on the creator's behalf.");
        }

        /// <summary>
        /// A helmet is one helmet, not a stack of ninety of them.
        /// </summary>
        /// <remarks>
        /// initialAmount is the durability an item starts with when it wears out and the number a
        /// player is handed when it stacks, and the game tells the two apart by isStackable alone.
        /// The tick starts life ticked, so a helmet taking it at its word was handed over ninety at
        /// a time. Not one of the game's 343 prefabs with a durability pool is stackable, and not
        /// one of its 90 helmets stacks.
        /// </remarks>
        [Test]
        public void AHelmet_IsOneHelmetRatherThanAStackOfNinety()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Armor,
                "mod_stacking_helm",
                DimensionWhatItIs.Helmet,
                serialized =>
                {
                    serialized.FindProperty("stackable").boolValue = true;
                    serialized.FindProperty("stackableWasMigrated").boolValue = true;
                });

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            GameObject prefab = LoadPrefab("mod_stacking_helm");
            Assert.That(prefab.GetComponent<ObjectAuthoring>().initialAmount, Is.EqualTo(90));
            Assert.That(
                prefab.GetComponent<InventoryItemAuthoring>().isStackable,
                Is.False,
                "A stackable helmet is a stack of ninety helmets, because initialAmount is both.");
            Assert.That(report.Derived, Has.Some.Contains("one per slot"));
        }

        /// <summary>
        /// The case that bakes int.MinValue if the fallback is skipped: a typed durability AND no
        /// cooldown. A flat number taking an early return past the cooldown fallback leaves the
        /// game's formula dividing 350 by zero on the next import and rounding infinity into the
        /// sign bit — a sword
        /// that reads as permanently reinforced, draws a negative bar and breaks on its first swing.
        /// </summary>
        [Test]
        public void AMeleeWeaponWithATypedDurabilityAndNoCooldown_IsStillTheGamesOwnNumber()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Weapon,
                "mod_typed_untimed_blade",
                DimensionWhatItIs.MeleeWeapon,
                serialized =>
                {
                    serialized.FindProperty("damageAmount").intValue = 5;
                    serialized.FindProperty("durabilityPoints").intValue = 250;
                });

            DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            GameObject prefab = LoadPrefab("mod_typed_untimed_blade");
            Assert.That(
                prefab.GetComponent<DurabilityAuthoring>().maxDurability,
                Is.EqualTo(350),
                "A melee weapon has a formula, and the formula wins over a typed number.");
            Assert.That(
                prefab.GetComponent<DurabilityAuthoring>().maxDurability,
                Is.GreaterThan(0),
                "int.MinValue is what a division by a zero cooldown rounds to.");
            CooldownAuthoring cooldown = prefab.GetComponent<CooldownAuthoring>();
            Assert.That(
                cooldown,
                Is.Not.Null,
                "The effects pass runs last and used to delete this component outright.");
            Assert.That(
                cooldown.cooldown,
                Is.EqualTo(DimensionItemObjectTypes.MeleeFallbackCooldown).Within(0.0001f));
        }

        /// <summary>
        /// A seeder is one of the kinds the game keeps no durability number for — both of vanilla's
        /// two carry 250 because their prefabs say 250. The typed number has to land in
        /// initialAmount, which is the only field the game's recompute reads back rather than
        /// overwrites.
        /// </summary>
        [Test]
        public void ASeeder_KeepsTheNumberTheAuthorTyped()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Tool,
                "mod_seeder",
                DimensionWhatItIs.Seeder,
                serialized =>
                {
                    serialized.FindProperty("cooldownSeconds").floatValue = 0.4f;
                    serialized.FindProperty("durabilityPoints").intValue = 250;
                });

            DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            GameObject prefab = LoadPrefab("mod_seeder");
            Assert.That(
                prefab.GetComponent<ObjectAuthoring>().objectType, Is.EqualTo(ObjectType.Seeder));
            Assert.That(
                prefab.GetComponent<ObjectAuthoring>().initialAmount,
                Is.EqualTo(250),
                "initialAmount is the input the game's own recompute reads.");
            Assert.That(prefab.GetComponent<DurabilityAuthoring>().maxDurability, Is.EqualTo(250));
        }

        /// <summary>And one with nothing typed is told it starts at a single use.</summary>
        [Test]
        public void ASeederWithNoNumberTyped_IsToldItBreaksOnItsFirstUse()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Tool,
                "mod_bare_seeder",
                DimensionWhatItIs.Seeder,
                serialized => serialized.FindProperty("cooldownSeconds").floatValue = 0.4f);

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(
                LoadPrefab("mod_bare_seeder").GetComponent<DurabilityAuthoring>().maxDurability,
                Is.EqualTo(1));
            Assert.That(report.Warnings, Has.Some.Contains("no durability of its own"));
        }

        /// <summary>
        /// A thrown weapon is a stack, not a pool. All seven of the game's stack, start at one and
        /// carry no durability component — and on a stackable item initialAmount IS the number
        /// handed over, so a computed 250 would deal out 250 knives at a time.
        /// </summary>
        [Test]
        public void AThrowingWeapon_IsAStackRatherThanADurabilityPool()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Weapon,
                "mod_knife",
                DimensionWhatItIs.ThrowingWeapon,
                serialized =>
                {
                    serialized.FindProperty("damageAmount").intValue = 11;
                    serialized.FindProperty("cooldownSeconds").floatValue = 0.6f;
                });

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            GameObject prefab = LoadPrefab("mod_knife");
            Assert.That(
                prefab.GetComponent<ObjectAuthoring>().objectType,
                Is.EqualTo(ObjectType.ThrowingWeapon));
            Assert.That(
                prefab.GetComponent<DurabilityAuthoring>(),
                Is.Null,
                "No vanilla throwing weapon carries a durability pool.");
            Assert.That(prefab.GetComponent<ObjectAuthoring>().initialAmount, Is.EqualTo(1));
            Assert.That(
                prefab.GetComponent<InventoryItemAuthoring>().isStackable,
                Is.True,
                "All seven of the game's throwing weapons stack, and MakeItem says they do not.");
            Assert.That(report.Derived, Has.Some.Contains("stack of one"));
        }

        /// <summary>
        /// Four of the game's systems read the type off the live object rather than the item table,
        /// and ObjectConverter — the converter every mod object goes through — never writes it. The
        /// marker is what carries it.
        /// </summary>
        [Test]
        public void EveryGeneratedItem_CarriesTheTypeOntoTheRunningObject()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Block, "mod_marked_block", DimensionWhatItIs.NotSaid);

            DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(
                LoadPrefab("mod_marked_block")
                    .GetComponent<ExpandNullforge.Authoring.DimensionObjectTypeAuthoring>(),
                Is.Not.Null,
                "Without this the object is invisible to EnvironmentalConditionsSystem, which "
                + "queries WithAll<ObjectTypeCD>() rather than reading a default.");
        }

        [Test]
        public void APickaxe_GetsTheEightHundredTheGameGivesOne()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Tool,
                "mod_pick",
                DimensionWhatItIs.Pickaxe,
                serialized => serialized.FindProperty("cooldownSeconds").floatValue = 0.6f);

            DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            GameObject prefab = LoadPrefab("mod_pick");
            Assert.That(
                prefab.GetComponent<ObjectAuthoring>().objectType,
                Is.EqualTo(ObjectType.MiningPick));
            Assert.That(
                prefab.GetComponent<DurabilityAuthoring>().maxDurability,
                Is.EqualTo(800),
                "A pickaxe is not divided by its cooldown; it is a flat 800 times the multiplier.");
        }

        /// <summary>
        /// The game has an empty durability case for a necklace on purpose, so one stays at the
        /// amount it was handed. That is the right answer, not a missing one.
        /// </summary>
        [Test]
        public void ANecklace_DoesNotWearOut()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Armor,
                "mod_charm",
                DimensionWhatItIs.Necklace);

            DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            GameObject prefab = LoadPrefab("mod_charm");
            Assert.That(
                prefab.GetComponent<ObjectAuthoring>().objectType, Is.EqualTo(ObjectType.Necklace));
            Assert.That(prefab.GetComponent<DurabilityAuthoring>().maxDurability, Is.EqualTo(1));
        }

        // ------------------------------------------- items written before this ---

        /// <summary>
        /// The no-regression case. An item authored before the question existed carries Not said, and
        /// has to generate the object it always generated: a material wrote no type at all, which
        /// the game reads as NonUsable.
        /// </summary>
        [Test]
        public void AMaterialThatNeverSaid_StillGeneratesAsNothingInParticular()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Material, "mod_old_dust", DimensionWhatItIs.NotSaid);

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(report.Errors, Is.Empty, string.Join("; ", report.Errors));
            Assert.That(
                LoadObject("mod_old_dust").objectType,
                Is.EqualTo(ObjectType.NonUsable),
                "This is the value the generator wrote for a material before the question existed.");
            Assert.That(
                report.Derived,
                Has.Some.Contains("nothing in particular"),
                "Working a kind out for somebody has to be readable, not silent.");
        }

        /// <summary>And a block still writes the placeable type it always wrote.</summary>
        [Test]
        public void ABlockThatNeverSaid_StillGeneratesAsSomethingYouPlace()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Block, "mod_old_block", DimensionWhatItIs.NotSaid);

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(
                LoadObject("mod_old_block").objectType, Is.EqualTo(ObjectType.PlaceablePrefab));
            Assert.That(report.Derived, Has.Some.Contains("something you place"));
        }

        /// <summary>
        /// An item that never answered in so many words but whose weapon block says it is swung has
        /// said which kind it is in everything but name.
        /// </summary>
        [Test]
        public void AWeaponThatNeverSaid_IsWorkedOutFromHowItSaysItAttacks()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Weapon,
                "mod_derived_blade",
                DimensionWhatItIs.NotSaid,
                serialized =>
                {
                    serialized.FindProperty("damageAmount").intValue = 8;
                    serialized.FindProperty("cooldownSeconds").floatValue = 0.4f;
                    serialized.FindProperty("weapon.kind").intValue = (int)DimensionWeaponKind.Melee;
                });

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(
                LoadObject("mod_derived_blade").objectType, Is.EqualTo(ObjectType.MeleeWeapon));
            Assert.That(report.Derived, Has.Some.Contains("how it says it attacks"));
        }

        /// <summary>And the same for a piece of armour that says where it is drawn.</summary>
        [Test]
        public void ArmourThatNeverSaid_IsWorkedOutFromWhereItIsDrawn()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Armor,
                "mod_derived_trousers",
                DimensionWhatItIs.NotSaid,
                serialized => serialized.FindProperty("equipmentSkin.slot").intValue =
                    (int)DimensionEquipmentSkinSlot.Legs);

            DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(
                LoadObject("mod_derived_trousers").objectType, Is.EqualTo(ObjectType.PantsArmor));
        }

        /// <summary>
        /// A tool template covers eleven things the game keeps apart. When nothing else on the item
        /// narrows it down, the author is told rather than handed a guess.
        /// </summary>
        [Test]
        public void AToolThatNeverSaid_IsReportedRatherThanGuessedAt()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Tool,
                "mod_vague_tool",
                DimensionWhatItIs.NotSaid,
                serialized => serialized.FindProperty("cooldownSeconds").floatValue = 0.5f);

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(LoadObject("mod_vague_tool").objectType, Is.EqualTo(ObjectType.NonUsable));
            Assert.That(report.Warnings, Has.Some.Contains("What it is"));
        }

        // ------------------------------------------------------ disagreements ---

        [Test]
        public void AWeaponKindOnATemplateWithNoDamage_IsReported()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Material,
                "mod_toothless_blade",
                DimensionWhatItIs.MeleeWeapon);

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(report.Warnings, Has.Some.Contains("hits for nothing"));
        }

        /// <summary>
        /// The placement check does not exempt Pet and Food from the rule it prints. The game routes
        /// a pet to the gear slots and food to the eating slot, and PlaceObjectSlot turns both away,
        /// so a pet on a placing template is exactly the case the line is for.
        /// </summary>
        [Test]
        public void APetOnAPlacingTemplate_IsReportedLikeAnyOtherKindThatCannotBePlaced()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Placeable, "mod_placed_pet", DimensionWhatItIs.Pet);

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(report.Warnings, Has.Some.Contains("never put anywhere"));
        }

        [Test]
        public void ABagKind_SaysItAddsNoSpaceYet()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Material, "mod_sack", DimensionWhatItIs.Bag);

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(LoadObject("mod_sack").objectType, Is.EqualTo(ObjectType.Bag));
            Assert.That(report.Warnings, Has.Some.Contains("extra inventory space"));
        }

        /// <summary>
        /// Food is the one kind the item does not get to assert on its own: the cooking answer sets
        /// the type and clears it again when it says no, so a bare claim of food is reported.
        /// </summary>
        [Test]
        public void FoodWithNoCookingAnswer_IsReported()
        {
            DimensionItemAsset item = MakeItem(
                DimensionItemArchetype.Consumable, "mod_phantom_meal", DimensionWhatItIs.Food);

            DimensionItemGenerationReport report =
                DimensionItemGenerator.Generate(new[] { item }, TestFolder);

            Assert.That(report.Warnings, Has.Some.Contains("Part in Cooking"));
        }

        // ------------------------------------------------------------ helpers ---

        private static void AssertKind(DimensionWhatItIs kind, ObjectType expected)
        {
            Assert.That(
                DimensionItemObjectTypes.ToObjectType(kind),
                Is.EqualTo(expected),
                DimensionWhatItIsRules.Describe(kind) + " must generate as " + expected + ".");
        }

        private static GameObject LoadPrefab(string fileName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                TestFolder + "/" + fileName + ".prefab");
            Assert.That(prefab, Is.Not.Null, fileName + " was not written.");
            return prefab;
        }

        private static ObjectAuthoring LoadObject(string fileName)
        {
            ObjectAuthoring authoring = LoadPrefab(fileName).GetComponent<ObjectAuthoring>();
            Assert.That(authoring, Is.Not.Null, fileName + " has no ObjectAuthoring.");
            return authoring;
        }

        private DimensionItemAsset MakeItem(
            DimensionItemArchetype archetype,
            string itemId,
            DimensionWhatItIs whatItIs,
            Action<SerializedObject> configure = null)
        {
            DimensionItemAsset item = ScriptableObject.CreateInstance<DimensionItemAsset>();
            created.Add(item);

            SerializedObject serialized = new SerializedObject(item);
            serialized.Update();
            serialized.FindProperty("itemId").stringValue = itemId;
            serialized.FindProperty("displayName").stringValue = itemId;
            serialized.FindProperty("iconId").stringValue = "icon:" + itemId;
            serialized.FindProperty("stackableWasMigrated").boolValue = true;
            serialized.FindProperty("stackable").boolValue = false;
            serialized.FindProperty("enabled").boolValue = true;
            serialized.FindProperty("archetype").intValue = (int)archetype;

            // intValue rather than enumValueIndex: both enums number their members explicitly, and
            // the raw value is the one that has to survive into the asset.
            serialized.FindProperty("whatItIs").intValue = (int)whatItIs;

            // Every archetype that attaches loot or health refuses to generate without them, so the
            // basics go in for all of them rather than one test at a time.
            serialized.FindProperty("lootTableId").stringValue = "None";
            serialized.FindProperty("healthPoints").intValue = 10;

            configure?.Invoke(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static void DeleteTestFolder()
        {
            if (AssetDatabase.IsValidFolder(TestFolder))
            {
                AssetDatabase.DeleteAsset(TestFolder);
                AssetDatabase.Refresh();
            }
        }
    }
}
#endif
