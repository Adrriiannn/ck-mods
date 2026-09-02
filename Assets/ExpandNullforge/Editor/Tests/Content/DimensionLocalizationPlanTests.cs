#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.Conditions;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the promise that "what the player sees" is a thing the player can actually see.
    /// </summary>
    /// <remarks>
    /// A generator that writes a prefab and no localization row leaves the
    /// player reading the key — "Items/MyMod_stoneBench" — in the tooltip where the name belongs.
    /// The failure is completely silent: the mod builds, loads, and plays. These tests hold both
    /// halves of the fix: that a row is written for each kind of content, and that the check which
    /// catches the next unnamed thing actually fires.
    /// </remarks>
    public sealed class DimensionLocalizationPlanTests
    {
        private readonly List<Object> made = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            for (int i = 0; i < made.Count; i++)
            {
                if (made[i] != null)
                {
                    Object.DestroyImmediate(made[i]);
                }
            }

            made.Clear();
            DimensionConditionRegistry.Clear();
        }

        // ---- the key rule -------------------------------------------------------------------

        /// <summary>
        /// Core Keeper rewrites a term with <c>Replace(':', '_')</c> before it searches, so a key
        /// stored with the colon an object name carries can never be found. Both key builders and
        /// the coverage check have to agree about that or the check reports rows that exist.
        /// </summary>
        [Test]
        public void AKeyIsWrittenWithTheUnderscoreTheGameLooksFor()
        {
            Assert.That(
                DimensionLocalizationCsv.ItemKeyFor("MyMod:stoneBench"),
                Is.EqualTo("Items/MyMod_stoneBench"));
            Assert.That(
                DimensionLocalizationCsv.NameKeyFor("MyMod:theWatcher"),
                Is.EqualTo("Names/MyMod_theWatcher"));
            Assert.That(DimensionLocalizationCsv.ItemKeyFor(null), Is.Empty);
            Assert.That(DimensionLocalizationCsv.NameKeyFor(string.Empty), Is.Empty);
        }

        /// <summary>
        /// A name with no colon in it is already its own lookup form, so nothing may change — a
        /// blanket rewrite would quietly rename every unqualified object.
        /// </summary>
        [Test]
        public void ANameWithNoQualifierIsLeftExactlyAsItIs()
        {
            Assert.That(DimensionLocalizationCsv.ItemKeyFor("Carrock"), Is.EqualTo("Items/Carrock"));
            Assert.That(
                DimensionLocalizationCsv.ToLookupKeyName("Carrock.ground.block"),
                Is.EqualTo("Carrock.ground.block"));
        }

        // ---- conditions ---------------------------------------------------------------------

        /// <summary>
        /// The buff UI asks for <c>"Conditions/" + conditionID.ToString()</c>, and a number a mod
        /// minted has no name in the enum, so the term really is the digits. Getting this key wrong
        /// leaves the buff nameless with nothing to show for it.
        /// </summary>
        [Test]
        public void AConditionsLineIsKeyedByTheNumberTheGameWillAskFor()
        {
            DimensionConditionAsset alpha = Condition("mod:aWind", "Windswept", "{0}% movement speed");
            DimensionConditionAsset beta = Condition("mod:bStone", "Stoneskin", "{0} armour");

            List<DimensionLocalizationCsv.Row> rows = RowsFor(alpha, beta);

            // Numbers are handed out in name order from the first free slot, so the pairing is
            // knowable without depending on which order the assets happened to sit in.
            int first = DimensionConditionRegistry.FirstFreeNumber;
            Assert.That(Text(rows, "Conditions/" + first), Is.EqualTo("{0}% movement speed"));
            Assert.That(Text(rows, "Conditions/" + (first + 1)), Is.EqualTo("{0} armour"));
        }

        /// <summary>
        /// There is no second row: the game gives a condition one piece of text and never appends
        /// "Desc" to it. Writing one would be a key nothing reads.
        /// </summary>
        [Test]
        public void AConditionGetsExactlyOneRowAndNoDescription()
        {
            List<DimensionLocalizationCsv.Row> rows =
                RowsFor(Condition("mod:aWind", "Windswept", "{0}% movement speed"));

            Assert.That(rows.Count, Is.EqualTo(1));
            Assert.That(rows[0].Key, Does.Not.EndWith("Desc"));
        }

        /// <summary>
        /// A condition nobody wrote a line for still has to read as something, and the fallback puts
        /// the number where the game will substitute it.
        /// </summary>
        [Test]
        public void AConditionWithNoLineOfItsOwnShowsItsNumberAndThenItsName()
        {
            List<DimensionLocalizationCsv.Row> rows =
                RowsFor(Condition("mod:aWind", "Windswept", string.Empty));

            Assert.That(rows[0].EnglishText, Is.EqualTo("{0} Windswept"));
        }

        // ---- every kind of content ----------------------------------------------------------

        /// <summary>
        /// The whole point: every family carries a name and a tooltip line, so none reaches the
        /// game nameless.
        /// </summary>
        [Test]
        public void EveryKindOfContentGetsItsNameAndItsTooltipLine()
        {
            DimensionTemplateAsset template = Template();
            SetArray(template, "globalWorkbenches", Workbench("bench", "Stone Bench", "Turns rocks into things."));
            SetArray(template, "globalContainers", Container("crate", "Crate", "It holds things."));
            SetArray(template, "globalWorldObjects", WorldObject("lamp", "Cave Lamp", "It glows."));
            SetArray(template, "globalPlants", Plant("crop", "Sunrice", "A grain."));
            SetArray(template, "globalMobs", Mob("stalker", "Stalker", "It follows."));
            SetArray(template, "globalAnimals", Animal("hog", "Cave Hog", "It snorts."));
            SetArray(template, "globalCritters", Critter("moth", "Dust Moth", "It flutters."));

            List<DimensionLocalizationCsv.Row> rows = Build(template).Rows;

            AssertNamed(rows, "Items/MyMod_bench", "Stone Bench", "Turns rocks into things.");
            AssertNamed(rows, "Items/MyMod_crate", "Crate", "It holds things.");
            AssertNamed(rows, "Items/MyMod_lamp", "Cave Lamp", "It glows.");
            AssertNamed(rows, "Items/MyMod_stalker", "Stalker", "It follows.");
            AssertNamed(rows, "Items/MyMod_hog", "Cave Hog", "It snorts.");
            AssertNamed(rows, "Items/MyMod_moth", "Dust Moth", "It flutters.");
        }

        /// <summary>
        /// The seed is the only part of a crop anybody holds, and it reads as the crop's name with
        /// the word Seed after it — which is how every one of the game's own crops reads.
        /// </summary>
        [Test]
        public void ACropIsNamedOnItsSeed()
        {
            DimensionTemplateAsset template = Template();
            SetArray(template, "globalPlants", Plant("crop", "Sunrice", "A grain."));

            List<DimensionLocalizationCsv.Row> rows = Build(template).Rows;

            Assert.That(Text(rows, "Items/MyMod_cropSeed"), Is.EqualTo("Sunrice Seed"));
            Assert.That(Text(rows, "Items/MyMod_cropSeedDesc"), Is.EqualTo("A grain."));

            // The growing plant is never named by the game, so writing a row for it would be a key
            // nothing reads — Core Keeper's own table has Carrock and CarrockSeed and no CarrockPlant.
            Assert.That(Has(rows, "Items/MyMod_cropPlant"), Is.False);
        }

        /// <summary>
        /// An elite is a second creature with an object of its own, so it needs a name of its own —
        /// without one the harder copy is the only thing left showing a raw key.
        /// </summary>
        [Test]
        public void AnEliteIsNamedSeparatelyFromTheCreatureItCopies()
        {
            DimensionMobAsset mob = Mob("stalker", "Stalker", "It follows.");
            SerializedObject serialized = new SerializedObject(mob);
            SerializedProperty elite = serialized.FindProperty("eliteVariant");
            elite.FindPropertyRelative("enabled").boolValue = true;
            elite.FindPropertyRelative("namePrefix").stringValue = "Dread";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionTemplateAsset template = Template();
            SetArray(template, "globalMobs", mob);

            List<DimensionLocalizationCsv.Row> rows = Build(template).Rows;

            Assert.That(Text(rows, "Items/MyMod_stalker"), Is.EqualTo("Stalker"));
            Assert.That(Text(rows, "Items/MyMod_stalker.elite"), Is.EqualTo("Dread Stalker"));
        }

        /// <summary>
        /// A workbench that only groups recipes onto somebody else's object builds no object of
        /// ours, so a row would name something we did not make.
        /// </summary>
        [Test]
        public void AWorkbenchThatOnlyGroupsRecipesGetsNoRow()
        {
            DimensionWorkbenchAsset workbench = Workbench("bench", "Stone Bench", string.Empty);
            SerializedObject serialized = new SerializedObject(workbench);
            serialized.FindProperty("generatesItsOwnObject").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionTemplateAsset template = Template();
            SetArray(template, "globalWorkbenches", workbench);

            Assert.That(Has(Build(template).Rows, "Items/MyMod_bench"), Is.False);
        }

        [Test]
        public void SomethingTurnedOffIsNotNamed()
        {
            DimensionContainerAsset container = Container("crate", "Crate", string.Empty);
            SerializedObject serialized = new SerializedObject(container);
            serialized.FindProperty("enabled").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionTemplateAsset template = Template();
            SetArray(template, "globalContainers", container);

            Assert.That(Has(Build(template).Rows, "Items/MyMod_crate"), Is.False);
        }

        /// <summary>
        /// A blank name would put an empty line in the tooltip, which is worse than the key it
        /// replaced. The id goes in instead and the author is told to replace it.
        /// </summary>
        [Test]
        public void AnObjectWithNoNameFallsBackToItsIdAndIsReported()
        {
            DimensionTemplateAsset template = Template();
            SetArray(template, "globalContainers", Container("crate", string.Empty, string.Empty));

            DimensionLocalizationPlan plan = Build(template);

            Assert.That(Text(plan.Rows, "Items/MyMod_crate"), Is.EqualTo("crate"));
            Assert.That(plan.Warnings.Count, Is.EqualTo(1));
            Assert.That(plan.Warnings[0], Does.Contain("crate"));
        }

        /// <summary>
        /// A vehicle is named like every other placeable, because it IS one.
        /// </summary>
        /// <remarks>
        /// A vehicle generated as
        /// <c>ObjectType.NonObtainable</c> with no placement never appears in a slot, and a row
        /// for it is a key nothing reads. A vehicle is <c>PlaceablePrefab</c> with an
        /// icon and placement, exactly as the game's own Boat and Minecart are, so the honest
        /// answer is a name.
        /// </remarks>
        [Test]
        public void AVehicleIsNamedLikeEveryOtherPlaceableObject()
        {
            DimensionVehicleAsset vehicle = ScriptableObject.CreateInstance<DimensionVehicleAsset>();
            made.Add(vehicle);
            SerializedObject serialized = new SerializedObject(vehicle);
            serialized.FindProperty("vehicleId").stringValue = "raft";
            serialized.FindProperty("displayName").stringValue = "Raft";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            DimensionTemplateAsset template = Template();
            SetArray(template, "globalVehicles", vehicle);

            DimensionLocalizationPlan plan = Build(template);

            Assert.That(Has(plan.Rows, "Items/MyMod_raft"), Is.True);
            Assert.That(plan.Warnings.Count, Is.EqualTo(0));
        }

        // ---- the coverage check --------------------------------------------------------------

        /// <summary>
        /// The regression guard itself. A generated object a player can hold, with no row, is the
        /// exact shape of the bug this whole pass fixed.
        /// </summary>
        [Test]
        public void AnObjectThePlayerCanHoldWithNoRowIsReportedByName()
        {
            List<string> warnings = Check(
                Written(),
                Generated("Assets/Mod/Items/bench.prefab", "MyMod:bench", ObjectType.PlaceablePrefab));

            Assert.That(warnings.Count, Is.EqualTo(1));
            Assert.That(warnings[0], Does.Contain("MyMod:bench"));
            Assert.That(
                warnings[0],
                Does.Contain("Items/MyMod_bench"),
                "The warning has to show the string the player would actually read.");
        }

        [Test]
        public void AnObjectWithItsRowIsNotReported()
        {
            List<string> warnings = Check(
                Written("Items/MyMod_bench"),
                Generated("Assets/Mod/Items/bench.prefab", "MyMod:bench", ObjectType.PlaceablePrefab));

            Assert.That(warnings, Is.Empty);
        }

        /// <summary>
        /// A boss is named through <c>Names/</c>, which is where the game reads a boss's name from.
        /// Demanding an <c>Items/</c> row of it would be demanding a key nothing reads.
        /// </summary>
        [Test]
        public void ABossNamedThroughNamesIsNotReported()
        {
            List<string> warnings = Check(
                Written("Names/MyMod_theWatcher"),
                Generated("Assets/Mod/Creatures/watcher.prefab", "MyMod:theWatcher", ObjectType.Creature));

            Assert.That(warnings, Is.Empty);
        }

        /// <summary>
        /// <c>NonObtainable</c> is the game's own way of saying an object has no tooltip: nobody can
        /// hold it or hover it, so there is nowhere for a name to appear.
        /// </summary>
        [Test]
        public void AnObjectNobodyCanHoldIsNotReported()
        {
            List<string> warnings = Check(
                Written(),
                Generated("Assets/Mod/Plants/cropPlant.prefab", "MyMod:cropPlant", ObjectType.NonObtainable));

            Assert.That(warnings, Is.Empty);
        }

        /// <summary>
        /// A custom ore vein deliberately carries the vanilla ore's own unqualified name, because it
        /// IS that ore. Core Keeper named it long ago.
        /// </summary>
        [Test]
        public void AnObjectCarryingAVanillaNameIsNotOursToName()
        {
            List<string> warnings = Check(
                Written(),
                Generated("Assets/Mod/Items/CopperOre.prefab", "CopperOre", ObjectType.NonUsable));

            Assert.That(warnings, Is.Empty);
        }

        /// <summary>
        /// A crop's versions are several prefabs sharing one object. Reporting the file rather than
        /// the object would bury the author in copies of one problem.
        /// </summary>
        [Test]
        public void OneObjectSpreadAcrossSeveralPrefabsIsReportedOnce()
        {
            List<string> warnings = Check(
                Written(),
                Generated("Assets/Mod/Plants/cropSeed.prefab", "MyMod:cropSeed", ObjectType.PlaceablePrefab),
                Generated("Assets/Mod/Plants/cropSeedGolden.prefab", "MyMod:cropSeed", ObjectType.PlaceablePrefab));

            Assert.That(warnings.Count, Is.EqualTo(1));
        }

        // ---- helpers ------------------------------------------------------------------------

        private static List<string> Check(
            ICollection<string> writtenKeys,
            params DimensionLocalizationCoverage.GeneratedObject[] objects)
        {
            List<string> warnings = new List<string>();
            DimensionLocalizationCoverage.Check(objects, writtenKeys, warnings);
            return warnings;
        }

        private static DimensionLocalizationCoverage.GeneratedObject Generated(
            string path,
            string objectName,
            ObjectType objectType)
        {
            return new DimensionLocalizationCoverage.GeneratedObject(path, objectName, objectType);
        }

        private static HashSet<string> Written(params string[] keys)
        {
            return new HashSet<string>(keys, System.StringComparer.Ordinal);
        }

        private static DimensionLocalizationPlan Build(DimensionTemplateAsset template)
        {
            return DimensionLocalizationPlan.Build(
                template,
                new DimensionNamingContext("MyMod", null));
        }

        /// <summary>
        /// Claims the conditions the way a generate does, so the numbers under test are the numbers
        /// the game will be handed.
        /// </summary>
        private List<DimensionLocalizationCsv.Row> RowsFor(params DimensionConditionAsset[] conditions)
        {
            DimensionTemplateAsset template = Template();
            SetArray(template, "globalConditions", conditions);

            using (new DimensionConditionScope(conditions))
            {
                return Build(template).Rows;
            }
        }

        private DimensionTemplateAsset Template()
        {
            DimensionTemplateAsset template = ScriptableObject.CreateInstance<DimensionTemplateAsset>();
            made.Add(template);
            return template;
        }

        private DimensionConditionAsset Condition(string id, string name, string line)
        {
            DimensionConditionAsset condition =
                ScriptableObject.CreateInstance<DimensionConditionAsset>();
            made.Add(condition);
            SerializedObject serialized = new SerializedObject(condition);
            serialized.FindProperty("conditionName").stringValue = id;
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("tooltipLine").stringValue = line;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return condition;
        }

        private DimensionWorkbenchAsset Workbench(string id, string name, string description)
        {
            DimensionWorkbenchAsset workbench =
                ScriptableObject.CreateInstance<DimensionWorkbenchAsset>();
            made.Add(workbench);
            Write(workbench, "workbenchId", id, name, description);
            return workbench;
        }

        private DimensionContainerAsset Container(string id, string name, string description)
        {
            DimensionContainerAsset container =
                ScriptableObject.CreateInstance<DimensionContainerAsset>();
            made.Add(container);
            Write(container, "containerId", id, name, description);
            return container;
        }

        private DimensionWorldObjectAsset WorldObject(string id, string name, string description)
        {
            DimensionWorldObjectAsset worldObject =
                ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            made.Add(worldObject);
            Write(worldObject, "objectIdentifier", id, name, description);
            return worldObject;
        }

        private DimensionPlantAsset Plant(string id, string name, string description)
        {
            DimensionPlantAsset plant = ScriptableObject.CreateInstance<DimensionPlantAsset>();
            made.Add(plant);
            Write(plant, "plantId", id, name, description);
            return plant;
        }

        private DimensionMobAsset Mob(string id, string name, string description)
        {
            DimensionMobAsset mob = ScriptableObject.CreateInstance<DimensionMobAsset>();
            made.Add(mob);
            Write(mob, "mobId", id, name, description);
            return mob;
        }

        private DimensionAnimalAsset Animal(string id, string name, string description)
        {
            DimensionAnimalAsset animal = ScriptableObject.CreateInstance<DimensionAnimalAsset>();
            made.Add(animal);
            Write(animal, "animalId", id, name, description);
            return animal;
        }

        private DimensionCritterAsset Critter(string id, string name, string description)
        {
            DimensionCritterAsset critter = ScriptableObject.CreateInstance<DimensionCritterAsset>();
            made.Add(critter);
            Write(critter, "critterId", id, name, description);
            return critter;
        }

        private static void Write(
            Object asset,
            string idField,
            string id,
            string name,
            string description)
        {
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty(idField).stringValue = id;
            serialized.FindProperty("displayName").stringValue = name;
            serialized.FindProperty("description").stringValue = description;
            serialized.FindProperty("enabled").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(DimensionTemplateAsset template, string field, params Object[] values)
        {
            SerializedObject serialized = new SerializedObject(template);
            SerializedProperty list = serialized.FindProperty(field);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssertNamed(
            List<DimensionLocalizationCsv.Row> rows,
            string key,
            string name,
            string description)
        {
            Assert.That(Text(rows, key), Is.EqualTo(name), key + " carries the wrong name.");
            Assert.That(
                Text(rows, key + "Desc"),
                Is.EqualTo(description),
                key + "Desc carries the wrong line.");
        }

        private static bool Has(List<DimensionLocalizationCsv.Row> rows, string key)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Key == key)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Text(List<DimensionLocalizationCsv.Row> rows, string key)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Key == key)
                {
                    return rows[i].EnglishText;
                }
            }

            Assert.Fail("No row was written for '" + key + "'.");
            return null;
        }
    }
}
#endif
