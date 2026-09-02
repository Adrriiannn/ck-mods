using System.Collections.Generic;
using System.IO;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using ExpandNullforge.Plants;
using NUnit.Framework;
using Pug.Sprite;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers what makes a generated crop visible, which for a long time nothing did.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EVERY FAILURE IN THIS AREA IS SILENT. A crop with one picture too few ripens showing its
    /// half-grown look; a version with no pictures of its own is a golden crop nobody can tell from
    /// an ordinary one; a prefab pointing at a shared body it has no registration for wears the
    /// last crop's art. None of them error, and all of them only show up in a real world with a
    /// real player standing over a real plot of soil.
    /// </para>
    /// <para>
    /// The stage count is tested past three on purpose. Core Keeper's own plant renderer holds four
    /// animations and reads off the end of that array above three looks, which is a limit the
    /// framework's own renderer does not have — and a test is the only thing that keeps that true
    /// if somebody later reaches for vanilla's fixed names.
    /// </para>
    /// </remarks>
    public sealed class DimensionPlantVisualTests
    {
        private const string TestRoot = "Assets/NullforgePlantVisualTests";

        private DimensionPlantAsset plant;

        [SetUp]
        public void Setup()
        {
            DimensionTestScratchFolder.Ensure(TestRoot);

            plant = ScriptableObject.CreateInstance<DimensionPlantAsset>();
            SetString("plantId", "testcrop");
            SetString("displayName", "Test Crop");
            SetString("produceItemId", "Carrock");
        }

        [TearDown]
        public void Cleanup()
        {
            if (plant != null)
            {
                Object.DestroyImmediate(plant);
            }

            DimensionTestScratchFolder.Remove(TestRoot);
        }

        // ---- how many pictures a crop needs ----

        [Test]
        public void ACropNeedsOnePictureMoreThanItHasGrowthStages()
        {
            // The off-by-one that costs people an evening: the game counts stages from zero up to
            // and including the top one, which is also why every vanilla plant prefab's
            // additionalSprites list is highestStage + 1 long.
            SetInt("growthStages", DimensionPlantAsset.VanillaGrowthStages);

            Assert.AreEqual(3, plant.PicturesNeeded);
        }

        [Test]
        public void ACropWithMoreThanThreeLooksStillGetsAnAnimationForEachOfThem()
        {
            SetInt("growthStages", 5);

            HashSet<int> hashes = new HashSet<int>();
            for (int i = 0; i < plant.PicturesNeeded; i++)
            {
                hashes.Add(SpriteAsset.StringToHash(
                    DimensionPlantSpriteAssetUtility.StageAnimationName(i)));
            }

            Assert.AreEqual(6, plant.PicturesNeeded, "five growth stages is six looks");
            Assert.AreEqual(
                6,
                hashes.Count,
                "Every stage needs an animation of its own. Core Keeper's own renderer stops at " +
                "four; this framework's does not, and that is the whole point of owning it.");
        }

        [Test]
        public void TheFirstThreeStagesAreSpelledTheWayTheGameSpellsThem()
        {
            Assert.AreEqual("stage1", DimensionPlantSpriteAssetUtility.StageAnimationName(0));
            Assert.AreEqual("stage2", DimensionPlantSpriteAssetUtility.StageAnimationName(1));
            Assert.AreEqual("stage3", DimensionPlantSpriteAssetUtility.StageAnimationName(2));
        }

        [Test]
        public void TheGeneratorAndTheRuntimeSpellTheDampSeedTheSameWay()
        {
            // The generator names the damp picture's file, and the runtime asks for it by the hash
            // of that name. Two spellings would mean a seed that never looks wet, with nothing said
            // about it anywhere.
            Assert.AreEqual(
                DimensionPlantSpriteAssetUtility.WateredVariantName,
                DimensionSeedView.WateredVariantName);
        }

        [Test]
        public void TheShadowIsTheSameBlackWhereverItIsPainted()
        {
            // The generator paints the shared body once and the view repaints every pooled instance;
            // a drift between them would make one crop's shadow sit differently on the ground from
            // the crop planted next to it.
            GameObject root = DimensionPlantViewBuilder.Build("PlantVisual", false);
            try
            {
                Assert.AreEqual(
                    DimensionPlantView.ShadowColour,
                    root.GetComponent<DimensionPlantView>().shadowSprite.color);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- where a picture sits on its tile ----

        [Test]
        public void ThePivotPinsThePictureItsGroundLineUpFromTheBottom()
        {
            // Four pixels, measured across plant_ripe, plant_stage3 and seedHeartBerry, whose
            // fractional pivots all work out to exactly that.
            Vector2 square = DimensionPlantSpriteAssetUtility.PivotFor(16, 16, 4);
            Vector2 tall = DimensionPlantSpriteAssetUtility.PivotFor(16, 32, 4);

            Assert.AreEqual(0.5f, square.x, 0.0001f, "always the middle across");
            Assert.AreEqual(0.25f, square.y, 0.0001f);
            Assert.AreEqual(
                0.125f,
                tall.y,
                0.0001f,
                "A taller picture keeps the same ground line, which is why it is counted in pixels.");
        }

        [Test]
        public void APivotIsAFractionOfOneFrameNotOfTheWholeRow()
        {
            // The eight-picture ripe row every vanilla crop waves through is 128 wide; each of its
            // frames is 16. Pinning against 128 would put the plant eight tiles to the left.
            Vector2 pivot = DimensionPlantSpriteAssetUtility.PivotFor(128 / 8, 16, 4);

            Assert.AreEqual(0.5f, pivot.x, 0.0001f);
            Assert.AreEqual(0.25f, pivot.y, 0.0001f);
        }

        // ---- addresses ----

        [Test]
        public void ArtAddressesAreTheSameEveryTimeTheyAreDerived()
        {
            // The generator writes the address and a completely separate step writes the
            // registration that has to find it. They never meet; they agree only by arithmetic.
            long low;
            long high;
            long lowAgain;
            long highAgain;
            DimensionPlantSpriteAssetUtility.AddressFor(
                DimensionPlantGenerator.PlantArtSeed("Mod:testcropPlant", null), out low, out high);
            DimensionPlantSpriteAssetUtility.AddressFor(
                DimensionPlantGenerator.PlantArtSeed("Mod:testcropPlant", null),
                out lowAgain, out highAgain);

            Assert.AreEqual(low, lowAgain);
            Assert.AreEqual(high, highAgain);
            Assert.AreNotEqual(0L, low);
        }

        [Test]
        public void ACropAVersionAndASeedAllGetAddressesOfTheirOwn()
        {
            HashSet<long> seen = new HashSet<long>
            {
                LowFor(DimensionPlantGenerator.PlantArtSeed("Mod:testcropPlant", null)),
                LowFor(DimensionPlantGenerator.PlantArtSeed("Mod:testcropPlant", "Golden")),
                LowFor(DimensionPlantGenerator.SeedArtSeed("Mod:testcropSeed", null)),
                LowFor(DimensionPlantGenerator.SeedArtSeed("Mod:testcropSeed", "Golden")),

                // A second mod with a crop of the same name must not land on the first mod's art,
                // which is why the seed is built from the object name rather than the plant id.
                LowFor(DimensionPlantGenerator.PlantArtSeed("Other:testcropPlant", null))
            };

            Assert.AreEqual(5, seen.Count, "two looks must never share one address");
        }

        private static long LowFor(string seed)
        {
            long low;
            long high;
            DimensionPlantSpriteAssetUtility.AddressFor(seed, out low, out high);
            return low;
        }

        // ---- what the runtime looks a crop up by ----

        [Test]
        public void EveryObjectAndVariationHasAKeyOfItsOwnIncludingTheCatchAll()
        {
            // The catch-all variation is negative, and the key folds a variation into the low half
            // of a long. A sign that leaked into the high half would make the catch-all for one
            // crop answer for a different crop entirely.
            HashSet<long> keys = new HashSet<long>
            {
                DimensionPlantPresentationRegistry.KeyFor(ObjectID.Carrock, 0),
                DimensionPlantPresentationRegistry.KeyFor(ObjectID.Carrock, 1),
                DimensionPlantPresentationRegistry.KeyFor(ObjectID.Carrock, 2),
                DimensionPlantPresentationRegistry.KeyFor(
                    ObjectID.Carrock, DimensionPlantPresentationDefinition.AnyVariation),
                DimensionPlantPresentationRegistry.KeyFor(ObjectID.CarrockSeed, 0),
                DimensionPlantPresentationRegistry.KeyFor(
                    ObjectID.CarrockSeed, DimensionPlantPresentationDefinition.AnyVariation)
            };

            Assert.AreEqual(6, keys.Count);
        }

        [Test]
        public void TheFirstVersionsLookSitsOnTheVariationsTheGamesGoldenCropsUse()
        {
            // The whole reason a version can have a look of its own: the game already tells a
            // golden crop apart by variation, and the framework's own registry is keyed by the same
            // pair. If these drifted, the golden plant would draw the ordinary crop's pictures.
            Assert.AreEqual(
                DimensionPlantAsset.RareSeedVariation,
                DimensionPlantGenerator.SeedVariationFor(0));
            Assert.AreEqual(
                DimensionPlantAsset.RarePlantVariation,
                DimensionPlantGenerator.PlantVariationFor(0));

            Assert.AreNotEqual(
                DimensionPlantGenerator.PlantVariationFor(0),
                DimensionPlantAsset.CompletePlantVariation,
                "The already-ripe copy must not sit where the first version does.");
            Assert.AreNotEqual(
                DimensionPlantGenerator.SeedVariationFor(1),
                DimensionPlantGenerator.SeedVariationFor(0),
                "Two versions on one variation would share a look and a prefab.");
        }

        [Test]
        public void ALookWithNoPicturesBehindItCountsAsNothingToDraw()
        {
            DimensionPlantPresentationDefinition look = Look(0L, 0L, 3);

            Assert.IsFalse(
                look.HasArt,
                "A row with no address must read as 'leave the pooled body alone' rather than as " +
                "'draw nothing', or a crop with no art would wear the last crop's pictures.");
        }

        [Test]
        public void AStageBeyondTheEndShowsTheLastPictureRatherThanThrowing()
        {
            // The case is a crop whose author removed a stage after a world existed, so plants
            // already in the ground carry a stage the pictures no longer have. This runs in
            // ManagedLateUpdate, where an exception stops every object on screen from drawing.
            DimensionPlantPresentationDefinition look = Look(1L, 2L, 3);

            Assert.AreEqual(look.AnimationForStage(2), look.AnimationForStage(7));
            Assert.AreEqual(look.AnimationForStage(0), look.AnimationForStage(-3));
        }

        [Test]
        public void AGlowOnlyWhenRipeStartsAtTheLastStageAndOtherwiseAtTheFirst()
        {
            DimensionPlantPresentationDefinition ripeOnly = Look(1L, 2L, 4, true);
            DimensionPlantPresentationDefinition always = Look(1L, 2L, 4, false);

            Assert.AreEqual(3, ripeOnly.FirstGlowingStage);
            Assert.AreEqual(0, always.FirstGlowingStage);
        }

        private static DimensionPlantPresentationDefinition Look(
            long low,
            long high,
            int stages,
            bool glowsOnlyWhenRipe = true)
        {
            int[] animations = new int[stages];
            for (int i = 0; i < stages; i++)
            {
                animations[i] = SpriteAsset.StringToHash(
                    DimensionPlantSpriteAssetUtility.StageAnimationName(i));
            }

            return new DimensionPlantPresentationDefinition(
                "Mod:testcropPlant",
                DimensionPlantPresentationDefinition.AnyVariation,
                low,
                high,
                animations,
                0,
                true,
                Color.white,
                Color.black,
                Color.black,
                glowsOnlyWhenRipe,
                false);
        }

        // ---- the shared bodies ----

        [Test]
        public void ThePlantBodyIsWiredTheWayVanillasIs()
        {
            GameObject root = DimensionPlantViewBuilder.Build("PlantVisual", false);
            try
            {
                DimensionPlantView view = root.GetComponent<DimensionPlantView>();

                Assert.IsNotNull(view, "no view component");
                Assert.IsNotNull(view.XScaler, "anything that reads facing writes to this unguarded");
                Assert.IsNotNull(view.plantSprite, "nothing to draw the plant with");
                Assert.IsNotNull(view.shadowSprite, "nothing to lay its shape on the ground");
                Assert.IsNotNull(view.glowSprite, "nothing for a glowing crop to light the cave with");
                Assert.IsNotNull(view.spriteObjects, "the game plays transform animations off this");
                Assert.AreEqual(2, view.spriteObjects.Count);
                Assert.IsTrue(
                    view.useSharedTransformAnimations,
                    "Left off, a plant that pops as it grows leaves its shadow behind.");
                Assert.IsFalse(
                    view.glowSprite.gameObject.activeSelf,
                    "A blob left on would light the cave around every plain crop.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TheSeedBodyIsItsOwnTypeSoItPoolsSeparately()
        {
            GameObject root = DimensionPlantViewBuilder.Build("SeedVisual", true);
            try
            {
                Assert.IsNotNull(
                    root.GetComponent<DimensionSeedView>(),
                    "Core Keeper pools these by component type, so sharing a type would put seeds " +
                    "and plants in one pool.");
                Assert.IsNull(
                    root.GetComponent<DimensionPlantView>().glowSprite,
                    "A seed in the soil has nothing to glow with.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // ---- what the generator writes ----

        [Test]
        public void ADrawnCropPointsEveryOneOfItsPrefabsAtTheSharedBody()
        {
            GivePictures(3);
            GiveSeedPicture();

            DimensionPlantGenerationReport report = Run();

            Assert.IsEmpty(report.Errors, string.Join("; ", report.Errors));
            GameObject plantBody = Load("/" + DimensionPlantViewBuilder.PlantVisualName);
            GameObject seedBody = Load("/" + DimensionPlantViewBuilder.SeedVisualName);
            Assert.IsNotNull(plantBody, "no shared plant body was written");
            Assert.IsNotNull(seedBody, "no shared seed body was written");

            Assert.AreEqual(
                plantBody,
                Graphical(DimensionPlantGenerator.PlantSuffix),
                "the growing plant has no body");
            Assert.AreEqual(
                plantBody,
                Graphical(DimensionPlantGenerator.RipeSuffix),
                "the ripe plant has no body");
            Assert.AreEqual(
                seedBody,
                Graphical(DimensionPlantGenerator.SeedSuffix),
                "the seed has no body, so a player sees bare soil until it sprouts");
        }

        [Test]
        public void ACropThatDrawsNothingPointsAtNoBodyAtAll()
        {
            // Not tidiness. The body is pooled, so a crop with no art of its own would be handed an
            // instance still wearing a neighbour's pictures and grow disguised as that crop.
            DimensionPlantGenerationReport report = Run();

            Assert.IsEmpty(report.Errors, string.Join("; ", report.Errors));
            Assert.IsNull(Graphical(DimensionPlantGenerator.PlantSuffix));
            Assert.IsNull(Graphical(DimensionPlantGenerator.SeedSuffix));
        }

        [Test]
        public void TheSpriteAssetCarriesOneAnimationPerStageAndTheTwinkle()
        {
            GivePictures(3);
            GiveShinePicture();

            Run();

            SpriteAsset asset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                TestRoot + "/" + DimensionPlantSpriteAssetUtility.ArtFolderName + "/" +
                DimensionPlantGenerator.PlantArtName("testcrop", null) + ".asset");
            Assert.IsNotNull(asset, "no sprite asset was written for the crop");

            SerializedObject serialized = new SerializedObject(asset);
            Assert.AreEqual(
                4,
                serialized.FindProperty("m_animations").arraySize,
                "three stages and a twinkle");

            long low;
            long high;
            DimensionPlantSpriteAssetUtility.AddressFor(
                DimensionPlantGenerator.PlantArtSeed("testcropPlant", null), out low, out high);
            Assert.AreEqual(
                low,
                serialized.FindProperty("m_address.m_low").longValue,
                "The address the registration will look the pictures up by.");
        }

        [Test]
        public void ASeedsSpriteAssetIsAStillPictureWithADampVersionOfItself()
        {
            GiveSeedPicture();
            GiveWateredSeedPicture();

            Run();

            SpriteAsset asset = AssetDatabase.LoadAssetAtPath<SpriteAsset>(
                TestRoot + "/" + DimensionPlantSpriteAssetUtility.ArtFolderName + "/" +
                DimensionPlantGenerator.SeedArtName("testcrop", null) + ".asset");
            Assert.IsNotNull(asset, "no sprite asset was written for the seed");

            SerializedObject serialized = new SerializedObject(asset);
            Assert.AreEqual(
                0,
                serialized.FindProperty("m_animations").arraySize,
                "a seed does not animate");
            Assert.IsNotNull(
                serialized.FindProperty("m_staticSpriteData.texture").objectReferenceValue,
                "the dry picture is the one the game asks for by default");
            Assert.AreEqual(
                1,
                serialized.FindProperty("m_staticVariants").arraySize,
                "the damp picture is a static variant, named off its own file");
        }

        // ---- what the generator says out loud ----

        [Test]
        public void ACropWithNoPicturesIsToldItWillBeInvisible()
        {
            DimensionPlantGenerationReport report = Run();

            Assert.IsTrue(
                Mentions(report, "invisible in the ground"),
                "The whole gap this closes: a crop that grows and is never seen.");
        }

        [Test]
        public void ACropOnePictureShortIsToldExactlyHowManyItNeeds()
        {
            SetInt("growthStages", 2);
            GivePictures(2);

            DimensionPlantGenerationReport report = Run();

            Assert.IsTrue(
                Mentions(report, "it needs 3 pictures"),
                "A list one short leaves a ripe crop showing its half-grown picture, silently.");
        }

        [Test]
        public void ACropWithNoSeedPictureIsToldWhatAPlayerWillSee()
        {
            GivePictures(3);

            DimensionPlantGenerationReport report = Run();

            Assert.IsTrue(Mentions(report, "see bare"));
        }

        [Test]
        public void ARowThatDoesNotDivideIntoItsPictureCountIsReported()
        {
            // The game slices a row by width alone, so a strip that does not divide plays a
            // fraction of each picture and drifts sideways as it goes.
            GivePictures(3);
            SetPicturesInRow(new[] { 1, 1, 3 });

            DimensionPlantGenerationReport report = Run();

            Assert.IsTrue(Mentions(report, "does not divide into 3 pictures"));
        }

        [Test]
        public void AVersionThatDrawsNothingOfItsOwnIsToldNobodyWillNoticeIt()
        {
            GivePictures(3);
            AddVersion("Golden");

            DimensionPlantGenerationReport report = Run();

            Assert.IsTrue(
                Mentions(report, "look exactly like the ordinary crop"),
                "The flagship ask is a look per version; a version with no look is the failure.");
        }

        [Test]
        public void AnUnknownRipeBurstFallsBackToLeavesAndSaysSo()
        {
            GivePictures(3);
            SetArtString("ripePuffId", "NotARealPuff");

            DimensionPlantGenerationReport report = Run();

            Assert.IsTrue(Mentions(report, "not one of the game's own bursts"));
        }

        [Test]
        public void VersionKeysCanBeAskedForWithoutReportingTheAnswersTwice()
        {
            // The bootstrap has to compute the same keys the generator did, and it must not repeat
            // the generator's warnings while doing it.
            AddVersion("Golden");

            List<string> warnings = new List<string>();
            string[] withReport = DimensionPlantGenerator.BuildVersionKeys(
                plant, plant.EnabledVersions, warnings);
            string[] silent = DimensionPlantGenerator.BuildVersionKeys(
                plant, plant.EnabledVersions, null);

            Assert.AreEqual(withReport, silent);
            Assert.AreEqual("Golden", silent[0]);
        }

        // ---- helpers ----

        private DimensionPlantGenerationReport Run()
        {
            return DimensionPlantGenerator.Generate(
                new List<DimensionPlantAsset> { plant },
                TestRoot,
                default(DimensionNamingContext));
        }

        private static bool Mentions(DimensionPlantGenerationReport report, string fragment)
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

        private static GameObject Load(string suffix)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + suffix + ".prefab");
        }

        private static GameObject Graphical(string suffix)
        {
            GameObject prefab = Load("/testcrop" + suffix);
            Assert.IsNotNull(prefab, "no prefab called testcrop" + suffix);
            return prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;
        }

        private void SetString(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(plant);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetInt(string field, int value)
        {
            SerializedObject serialized = new SerializedObject(plant);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetArtString(string field, string value)
        {
            SerializedObject serialized = new SerializedObject(plant);
            serialized.FindProperty("art").FindPropertyRelative(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void GivePictures(int count)
        {
            SerializedObject serialized = new SerializedObject(plant);
            SerializedProperty pictures =
                serialized.FindProperty("art").FindPropertyRelative("growthPictures");
            pictures.arraySize = count;
            for (int i = 0; i < count; i++)
            {
                pictures.GetArrayElementAtIndex(i).objectReferenceValue =
                    Picture("stage" + i, 16, 16);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetPicturesInRow(int[] counts)
        {
            SerializedObject serialized = new SerializedObject(plant);
            SerializedProperty rows =
                serialized.FindProperty("art").FindPropertyRelative("picturesInEachRow");
            rows.arraySize = counts.Length;
            for (int i = 0; i < counts.Length; i++)
            {
                rows.GetArrayElementAtIndex(i).intValue = counts[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void GiveSeedPicture()
        {
            SetPicture("seedPicture", Picture("seed", 8, 8));
        }

        private void GiveWateredSeedPicture()
        {
            SetPicture("seedInWetGroundPicture", Picture("seedwet", 8, 8));
        }

        private void GiveShinePicture()
        {
            SetPicture("shinePicture", Picture("shine", 64, 16));
        }

        private void SetPicture(string field, Texture2D texture)
        {
            SerializedObject serialized = new SerializedObject(plant);
            serialized.FindProperty("art").FindPropertyRelative(field).objectReferenceValue = texture;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void AddVersion(string versionName)
        {
            SerializedObject serialized = new SerializedObject(plant);
            SerializedProperty list = serialized.FindProperty("versions");
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            SerializedProperty row = list.GetArrayElementAtIndex(index);
            row.FindPropertyRelative("versionName").stringValue = versionName;
            row.FindPropertyRelative("chancePercent").floatValue = 3f;
            row.FindPropertyRelative("usesTheGamesGoldenChance").boolValue = true;
            row.FindPropertyRelative("produceItemId").stringValue = string.Empty;
            row.FindPropertyRelative("harvestAmount").intValue = 0;
            row.FindPropertyRelative("chanceToGetThingsBackPercent").floatValue = 0f;
            row.FindPropertyRelative("enabled").boolValue = true;
            row.FindPropertyRelative("extraDrops").arraySize = 0;

            SerializedProperty look = row.FindPropertyRelative("look");
            look.FindPropertyRelative("growthPictures").arraySize = 0;
            look.FindPropertyRelative("shinePicture").objectReferenceValue = null;
            look.FindPropertyRelative("seedPicture").objectReferenceValue = null;
            look.FindPropertyRelative("seedInWetGroundPicture").objectReferenceValue = null;
            look.FindPropertyRelative("colourWash").colorValue = Color.white;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A real PNG on disk, because the generator refuses anything else.
        /// </summary>
        /// <remarks>
        /// Deliberately not an in-memory texture. Every picture is COPIED into the mod's own art
        /// folder under a name the game reads the animation's name out of, and a texture with no
        /// file behind it cannot be copied — which is a refusal worth exercising rather than
        /// working around.
        /// </remarks>
        private static Texture2D Picture(string name, int width, int height)
        {
            string path = TestRoot + "/" + name + ".png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
            {
                return existing;
            }

            Texture2D made = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.green;
            }

            made.SetPixels(pixels);
            made.Apply();
            byte[] bytes = made.EncodeToPNG();
            Object.DestroyImmediate(made);

            string absolute = DimensionSpriteArtFileUtility.AssetPathToAbsolutePath(path);
            File.WriteAllBytes(absolute, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
