using System.Collections.Generic;
using ExpandNullforge.Authoring;
using ExpandNullforge.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ExpandNullforge.EditorTests
{
    /// <summary>
    /// Covers the words that hang in the air: the writing on a sign, the name on a chest, the tag
    /// over an animal, and a boss's title.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EVERY EARLIER PASS RECORDED THIS AS IMPOSSIBLE, and the reason given was that a
    /// <c>PugText</c> needs a <c>PugFont</c> asset no mod ships. It does not.
    /// <c>PugText.font</c> is <c>[NonSerialized]</c>, so no prefab in the game carries one either;
    /// <c>PugText.Render</c> calls <c>SetFont</c> first, which pulls the face from the game's own
    /// <c>TextManager</c>. These tests exist so that conclusion cannot come back: they pin the
    /// wiring, and each one names the vanilla prefab its expected value was measured from.
    /// </para>
    /// <para>
    /// WHAT CANNOT BE TESTED HERE is whether the glyphs appear, because rendering needs
    /// <c>Manager.text</c> and there is no running game in the editor. So the tests pin the three
    /// things that decide whether they CAN appear: the component is wired to the field the game
    /// reads, its GameObject is on the layer <c>PugFont.Render</c> stamps onto every glyph, and it
    /// has the parent transform both floating-text components write to without a null check.
    /// </para>
    /// </remarks>
    public sealed class DimensionFloatingTextTests
    {
        private const string TestRoot = "Assets/NullforgeFloatingTextTests";

        /// <summary>The <c>WorldUI</c> layer, measured on four vanilla prefabs.</summary>
        private const int WorldUiLayer = 18;

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

        // ---- a sign a player can read from across the room ----

        [Test]
        public void ASignCarriesTheWritingThatFloatsAboveIt()
        {
            GameObject visual = Visual(Build("readsign", DimensionUseBehaviour.ReadLikeASign, 0.75f));

            WorldLabel sign = visual.GetComponent<WorldLabel>();
            Assert.IsNotNull(sign, "a sign must carry the framework's own WorldLabel view");
            Assert.IsNotNull(
                sign.worldLabel,
                "without this field WorldLabel.UpdateTextLabel does nothing and the sign is blank");

            AssertItCanRender(sign.worldLabel);
        }

        [Test]
        public void AChestCarriesTheNameThatFloatsAboveIt()
        {
            // Chest : WorldLabel, so this is the same field on the same base class. It is not only
            // decoration: Chest.Use refuses to make the chest the player's active world label
            // unless worldLabel is non-null, and the naming box in the chest window writes to the
            // active world label and nowhere else.
            GameObject visual = Visual(Build("namedchest", DimensionUseBehaviour.OpensLikeAChest));

            WorldLabel chest = visual.GetComponent<WorldLabel>();
            Assert.IsNotNull(chest);
            Assert.IsNotNull(
                chest.worldLabel,
                "without this the chest window's name box saves nothing a player types");

            AssertItCanRender(chest.worldLabel);
        }

        [Test]
        public void TheWordsAreStyledTheWayTheGamesOwnChestAndSignAre()
        {
            // Measured on Chest.prefab and SignText.prefab, which carry identical text settings. A
            // default-constructed PugTextStyle is bold large, white, no outline and no sorting
            // layer, which renders somewhere nobody has ever looked at.
            PugText text = Visual(Build("styledsign", DimensionUseBehaviour.ReadLikeASign))
                .GetComponent<WorldLabel>()
                .worldLabel;

            Assert.AreEqual(TextManager.FontFace.thinSmall, text.style.fontFace);
            Assert.AreEqual(
                PugTextStyle.HorizontalAlignment.center, text.style.horizontalAlignment);
            Assert.AreEqual(SortingLayer.NameToID("Front"), text.style.sortingLayer);
            Assert.AreEqual(9999, text.style.orderInLayer);
            Assert.IsTrue(
                text.style.UsesOutlines,
                "unoutlined light grey over a light floor is unreadable, which is why vanilla " +
                "outlines every side of it");
            Assert.IsTrue(text.usePooledResources, "a room of named chests must not allocate");
        }

        [Test]
        public void HowHighTheWordsFloatIsTheAuthorsAnswer()
        {
            PugText low = Visual(Build("lowsign", DimensionUseBehaviour.ReadLikeASign, 0.375f))
                .GetComponent<WorldLabel>().worldLabel;
            PugText high = Visual(Build("highsign", DimensionUseBehaviour.ReadLikeASign, 1.5f))
                .GetComponent<WorldLabel>().worldLabel;

            Assert.AreEqual(0.375f, low.transform.localPosition.y, 0.0001f);
            Assert.AreEqual(1.5f, high.transform.localPosition.y, 0.0001f);
        }

        // ---- an animal with a name over it ----

        [Test]
        public void AnAnimalCarriesTheNameTagAndCanHoldAName()
        {
            GameObject prefab = Build("namedgoat", DimensionUseBehaviour.TendedLikeAnAnimal, 2f);
            GameObject visual = Visual(prefab);

            Cattle animal = visual.GetComponent<Cattle>();
            Assert.IsNotNull(animal);
            Assert.IsNotNull(
                animal.nameTag,
                "Cattle.UpdateName, OnShow and OnHide all dereference this every frame");
            Assert.IsNotNull(animal.nameTag.text, "the tag has no words in it");
            AssertItCanRender(animal.nameTag.text);

            // Backwards-looking and vanilla's own wiring, measured on Camel.prefab: the tag sits on
            // the outer object and its 'container' points at the inner one holding the text. Wired
            // the other way round, hiding the interface hides the object the tag lives on and its
            // Awake and LateUpdate never run again.
            Assert.AreSame(
                animal.nameTag.text.gameObject,
                animal.nameTag.container,
                "the tag must hide the words, not itself");

            // CattleUI.SetName sends the name with no check, and the server answers with
            // GetComponentData<NameCD>, which THROWS when the component is absent. So this is not a
            // greyed-out box: it is a server exception the first time anybody names the animal.
            Assert.IsNotNull(
                prefab.GetComponent<NameAuthoring>(),
                "an animal that is tended must be able to hold the name the window offers it");
        }

        [Test]
        public void ANameTagLeftInsideTheAnimalSaysSo()
        {
            DimensionWorldObjectGenerationReport report;
            Build("sunkgoat", DimensionUseBehaviour.TendedLikeAnAnimal, 0.375f, out report);

            Assert.IsTrue(
                report.Warnings.Exists(w => w.Contains("inside most animals")),
                "half a tile is a chest's height, and half a tile up an animal is its middle: " +
                string.Join("; ", report.Warnings.ToArray()));
        }

        [Test]
        public void ANameTagAtASensibleHeightSaysNothing()
        {
            DimensionWorldObjectGenerationReport report;
            Build("finegoat", DimensionUseBehaviour.TendedLikeAnAnimal, 2f, out report);

            Assert.IsFalse(
                report.Warnings.Exists(w => w.Contains("inside most animals")),
                "how high a name belongs over a creature is the author's judgement above half a " +
                "tile: " + string.Join("; ", report.Warnings.ToArray()));
        }

        // ---- and nothing floats over the four uses that never had any ----

        [Test]
        public void TheUsesWithNoWordsAreGivenNone()
        {
            // The game's own crafting bench, character and vending machine carry no floating text
            // at all. Adding it because we now can would be inventing behaviour, and every PugText
            // costs a per-frame late update.
            AssertNothingFloats(DimensionUseBehaviour.OpensACraftingBench, "bench");
            AssertNothingFloats(DimensionUseBehaviour.TalkedToLikeAnNpc, "character");
            AssertNothingFloats(DimensionUseBehaviour.SellsLikeAShop, "shop");
        }

        // ---- somewhere to keep the words ----

        [Test]
        public void AChestKeepsTheStoreThatMakesItNameable()
        {
            // The correction this pins. The words are a DescriptionBuffer on the entity, and
            // Chest.Use checks for it before making the chest nameable. The old rule stripped
            // DescriptionAuthoring off everything that was not a sign, which took it straight back
            // off every container the spine had just given one to.
            GameObject prefab = Build("storechest", DimensionUseBehaviour.OpensLikeAChest);

            Assert.IsNotNull(
                prefab.GetComponent<DescriptionAuthoring>(),
                "a chest with nowhere to keep a name cannot be named");
        }

        [Test]
        public void AuthoredWordsSurviveAUseThatShowsNone()
        {
            // Generation stays authoritative about the EMPTY store, but an author's own text is
            // never silently deleted: DimensionObjectSpine writes 'text it comes with' into
            // initialText before the interaction pass runs, and a surface that promises and does
            // not deliver is the one thing this framework refuses to ship.
            GameObject prefab = Build(
                "shopwithwords",
                DimensionUseBehaviour.SellsLikeAShop,
                DimensionFloatingTextUtilityDefaults.HeightAboveAnObject,
                "Open all hours");

            DescriptionAuthoring words = prefab.GetComponent<DescriptionAuthoring>();
            Assert.IsNotNull(words, "the author's own text was thrown away");
            Assert.AreEqual("Open all hours", words.initialText);
        }

        [Test]
        public void AnEmptyStoreIsStillDroppedByAUseThatShowsNoWords()
        {
            GameObject prefab = Build("quietshop", DimensionUseBehaviour.SellsLikeAShop);

            Assert.IsNull(
                prefab.GetComponent<DescriptionAuthoring>(),
                "generation is authoritative: an object that shows no words and has none keeps no " +
                "store for them");
        }

        // ---- a container's own label ----

        [Test]
        public void AContainersAuthoredLabelReachesTheObject()
        {
            DimensionContainerAsset container =
                ScriptableObject.CreateInstance<DimensionContainerAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(container);
                serialized.FindProperty("containerId").stringValue = "labelledbox";
                serialized.FindProperty("displayName").stringValue = "Labelled Box";
                serialized.FindProperty("labelItComesWith").stringValue = "Seeds";
                serialized
                    .FindProperty("interaction")
                    .FindPropertyRelative("whatUsingItDoes")
                    .intValue = (int)DimensionUseBehaviour.OpensLikeAChest;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                DimensionContainerGenerator.Generate(
                    new List<DimensionContainerAsset> { container },
                    TestRoot,
                    default(DimensionNamingContext));

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(TestRoot + "/labelledbox.prefab");
                Assert.IsNotNull(prefab, "no container prefab was generated");

                DescriptionAuthoring label = prefab.GetComponent<DescriptionAuthoring>();
                Assert.IsNotNull(label, "a labelled container needs somewhere to keep the label");
                Assert.AreEqual(
                    "Seeds",
                    label.initialText,
                    "the authored label never reached the object, so it would float nothing");

                GameObject visual = prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;
                Assert.IsNotNull(visual);
                Assert.IsNotNull(
                    visual.GetComponent<WorldLabel>().worldLabel,
                    "a label with nothing to render it into is a label nobody sees");
            }
            finally
            {
                Object.DestroyImmediate(container);
            }
        }

        // ---- a boss's title ----

        [Test]
        public void ABossTitleSitsOnTheLayerTheGlyphsAreStampedWith()
        {
            // The regression this pins is silent and was shipped: the plate was built flat onto the
            // view root, so it kept the default layer, and PugFont.Render reads the PugText's own
            // GameObject layer once and stamps it onto every glyph it pools. It was also at y -0.5,
            // roughly at the boss's feet.
            DimensionCreatureGenerationReport report = DimensionCreatureGenerator.Generate(
                new List<DimensionCreatureGenerator.Request>
                {
                    new DimensionCreatureGenerator.Request
                    {
                        CreatureId = "titledboss",
                        DisplayName = "Titled Boss",
                        IsEnemy = true,
                        IsBoss = true,
                        Enabled = true
                    }
                },
                TestRoot,
                default(DimensionNamingContext));

            string path = report.Created.Count > 0
                ? report.Created[0]
                : (report.Updated.Count > 0 ? report.Updated[0] : null);
            Assert.IsNotNull(path, "no boss prefab was generated");

            GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                .GetComponent<ObjectAuthoring>()
                .graphicalPrefab;
            Assert.IsNotNull(visual, "a boss with nothing to draw has nothing to name");

            ExpandNullforge.Creatures.DimensionBossView view =
                visual.GetComponent<ExpandNullforge.Creatures.DimensionBossView>();
            Assert.IsNotNull(view);
            Assert.IsNotNull(view.nameText, "a boss with no nameplate to render into");

            AssertItCanRender(view.nameText);
            Assert.IsTrue(
                view.nameText.localize,
                "a boss's name is a localization term, not the raw string");
        }

        // ---- shared checks ----

        /// <summary>
        /// The three things that decide whether a PugText can draw anything at all, none of which
        /// the editor can prove by rendering.
        /// </summary>
        private static void AssertItCanRender(PugText text)
        {
            Assert.AreEqual(
                WorldUiLayer,
                text.gameObject.layer,
                "PugFont.Render stamps this layer onto every glyph it pools, so a text on the " +
                "default layer draws its letters where the game does not look for them");

            Assert.IsNotNull(
                text.transform.parent,
                "WorldLabel.UpdateWorldText and ObjectNameTag.Awake both write to the text's " +
                "PARENT transform with no null check");

            Assert.IsTrue(
                text.keepEnabledOnStart || text.renderOnStart,
                "PugText.Start deactivates its own GameObject when neither is set, so the words " +
                "would switch themselves off before anything asked for them");
        }

        private void AssertNothingFloats(DimensionUseBehaviour use, string what)
        {
            GameObject visual = Visual(Build("silent" + (int)use, use));

            Assert.IsNull(
                visual.GetComponentInChildren<PugText>(true),
                "the game's own " + what + " carries no floating text, and every one costs a " +
                "per-frame late update");
        }

        // ---- harness ----

        private static GameObject Visual(GameObject prefab)
        {
            Assert.IsNotNull(prefab, "nothing was generated");
            GameObject visual = prefab.GetComponent<ObjectAuthoring>().graphicalPrefab;
            Assert.IsNotNull(visual, "there is nothing for a player to walk up to");
            return visual;
        }

        private GameObject Build(
            string id,
            DimensionUseBehaviour use,
            float howHigh = DimensionFloatingTextUtilityDefaults.HeightAboveAnObject,
            string words = "")
        {
            DimensionWorldObjectGenerationReport ignored;
            return Build(id, use, howHigh, words, out ignored);
        }

        private GameObject Build(
            string id,
            DimensionUseBehaviour use,
            float howHigh,
            out DimensionWorldObjectGenerationReport report)
        {
            return Build(id, use, howHigh, string.Empty, out report);
        }

        private GameObject Build(
            string id,
            DimensionUseBehaviour use,
            float howHigh,
            string words,
            out DimensionWorldObjectGenerationReport report)
        {
            DimensionWorldObjectAsset asset =
                ScriptableObject.CreateInstance<DimensionWorldObjectAsset>();
            try
            {
                SerializedObject serialized = new SerializedObject(asset);
                serialized.FindProperty("objectIdentifier").stringValue = id;
                serialized.FindProperty("displayName").stringValue = id;
                serialized.FindProperty("textItComesWith").stringValue = words;

                SerializedProperty interaction = serialized.FindProperty("interaction");
                // intValue rather than enumValueIndex: enumValueIndex writes the ORDINAL POSITION,
                // which quietly picks a different use the moment the enum has explicit numbers.
                interaction.FindPropertyRelative("whatUsingItDoes").intValue = (int)use;
                interaction.FindPropertyRelative("howHighTheWordsFloat").floatValue = howHigh;
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
    }

    /// <summary>
    /// The default heights, repeated here because the builder that owns them is internal to the
    /// editor tools assembly and a test may not name a constant it cannot see.
    /// </summary>
    internal static class DimensionFloatingTextUtilityDefaults
    {
        /// <summary><c>Chest.prefab</c>'s height, and the authoring default.</summary>
        public const float HeightAboveAnObject = 0.375f;
    }
}
